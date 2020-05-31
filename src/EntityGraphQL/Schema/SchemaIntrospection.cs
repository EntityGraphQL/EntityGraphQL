namespace EntityGraphQL.Schema
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using EntityGraphQL.Extensions;
    using EntityGraphQL.Schema.Models;

    public static class SchemaIntrospection
    {
        /// <summary>
        /// Creates an Introspection schema
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="typeMappings"></param>
        /// <returns></returns>
        public static Schema Make(ISchemaProvider schema, CombinedMapping combinedMapping)
        {
            var types = new List<TypeElement>
            {
                new TypeElement
                {
                    Description = "The query type, represents all of the entry points into our object graph",
                    Kind = "OBJECT",
                    Name = "Query",
                    OfType = null,
                },
                new TypeElement
                {
                    Description = "The mutation type, represents all updates we can make to our data",
                    Kind = "OBJECT",
                    Name = "Mutation",
                    OfType = null,
                },
            };
            types.AddRange(BuildQueryTypes(schema, combinedMapping));
            types.AddRange(BuildInputTypes(schema, combinedMapping));
            types.AddRange(BuildEnumTypes(schema, combinedMapping));
            types.AddRange(BuildScalarTypes(schema, combinedMapping));

            var schemaDescription = new Schema
            {
                QueryType = new TypeElement
                {
                    Name = "Query"
                },
                MutationType = new TypeElement
                {
                    Name = "Mutation"
                },
                Types = types.OrderBy(x => x.Name).ToList(),
                Directives = BuildDirectives(schema, combinedMapping)
            };

            return schemaDescription;
        }

        private static IEnumerable<TypeElement> BuildScalarTypes(ISchemaProvider schema, CombinedMapping combinedMapping)
        {
            var types = new List<TypeElement>();

            foreach (var customScalar in schema.GetScalarTypes())
            {
                var typeElement = new TypeElement
                {
                    Kind = "SCALAR",
                    Name = customScalar.Name,
                    Description = null,
                };

                types.Add(typeElement);
            }

            return types;
        }

        private static List<TypeElement> BuildQueryTypes(ISchemaProvider schema, CombinedMapping combinedMapping)
        {
            var types = new List<TypeElement>();

            foreach (var st in schema.GetNonContextTypes().Where(s => !s.IsInput && !s.IsEnum && !s.IsScalar))
            {
                var typeElement = new TypeElement
                {
                    Kind = "OBJECT",
                    Name = st.Name,
                    Description = st.Description
                };

                types.Add(typeElement);
            }

            return types;
        }

        /// <summary>
        /// Build INPUT Type to be used by Mutations
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="combinedMapping"></param>
        /// <remarks>
        /// Since Types and Inputs cannot have the same name, camelCase the name to prevent duplicates.
        /// </remarks>
        /// <returns></returns>
        private static List<TypeElement> BuildInputTypes(ISchemaProvider schema, CombinedMapping combinedMapping)
        {
            var types = new List<TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsInput))
            {
                if (schemaType.Name.StartsWith("__"))
                    continue;

                var inputValues = new List<InputValue>();
                foreach (Field field in schemaType.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    // Skip any property with special attribute
                    var property = schemaType.ContextType.GetProperty(field.Name);
                    if (property != null && GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(property))
                        continue;

                    // Skipping custom fields added to schema
                    if (field.Resolve.NodeType == System.Linq.Expressions.ExpressionType.Call)
                        continue;

                    // Skipping ENUM type
                    if (field.ReturnTypeClr.GetTypeInfo().IsEnum)
                        continue;

                    inputValues.Add(new InputValue
                    {
                        Name = field.Name,
                        Description = field.Description,
                        Type = BuildType(schema, field.ReturnTypeClr, field.GetReturnType(schema), combinedMapping, true)
                    });
                }

                var typeElement = new TypeElement
                {
                    Kind = "INPUT_OBJECT",
                    Name = schemaType.Name,
                    Description = schemaType.Description,
                    InputFields = inputValues.ToArray()
                };

                types.Add(typeElement);
            }

            return types;
        }

        private static List<TypeElement> BuildEnumTypes(ISchemaProvider schema, CombinedMapping combinedMapping)
        {
            var types = new List<TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsEnum))
            {
                var typeElement = new TypeElement
                {
                    Kind = "ENUM",
                    Name = schemaType.Name,
                    Description = schemaType.Description,
                    EnumValues = new EnumValue[] { }
                };
                if (schemaType.Name.StartsWith("__"))
                    continue;

                var enumTypes = new List<EnumValue>();

                //filter to ENUM type ONLY!
                foreach (Field field in schemaType.GetFields())
                {
                    enumTypes.Add(new EnumValue
                    {
                        Name = field.Name,
                        Description = field.Description,
                        IsDeprecated = false,
                        DeprecationReason = null
                    });
                }

                typeElement.EnumValues = enumTypes.ToArray();
                if (typeElement.EnumValues.Count() > 0)
                    types.Add(typeElement);
            }

            return types;
        }

        private static TypeElement BuildType(ISchemaProvider schema, Type clrType, string gqlTypeName, CombinedMapping combinedMapping, bool isInput = false)
        {
            // Is collection of objects?
            var type = new TypeElement();
            if (clrType.IsEnumerableOrArray())
            {
                type.Kind = "LIST";
                type.Name = null;
                type.OfType = BuildType(schema, clrType.GetEnumerableOrArrayType(), gqlTypeName, combinedMapping, isInput);
            }
            else if (clrType.Name == "EntityQueryType`1")
            {
                type.Kind = "SCALAR";
                type.Name = "String";
                type.OfType = null;
            }
            else if (clrType.GetTypeInfo().IsEnum)
            {
                type.Kind = "ENUM";
                type.Name = FindNamedMapping(clrType, combinedMapping, gqlTypeName);
                type.OfType = null;
            }
            else
            {
                // ConvertGqlRequiredOrList below handles NON_NULL by type mappings
                if (clrType.IsNullableType() || clrType.Name == "RequiredField`1")
                {
                    clrType = clrType.GetGenericArguments()[0];
                }

                type.Kind = combinedMapping.TypeIsScalar(clrType) ? "SCALAR" : "OBJECT";
                type.OfType = null;
                if (type.Kind == "OBJECT" && isInput)
                {
                    type.Kind = "INPUT_OBJECT";
                }
                type.Name = FindNamedMapping(clrType, combinedMapping, gqlTypeName);

                type = ConvertGqlRequiredOrList(type);
            }

            return type;
        }

        /// <summary>
        /// mapped types are in GQL form e.g. [int!]!
        /// this could be a lot better
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static TypeElement ConvertGqlRequiredOrList(TypeElement type)
        {
            if (type.Name.EndsWith("!"))
            {
                return new TypeElement
                {
                    Kind = "NON_NULL",
                    Name = null,
                    OfType = ConvertGqlRequiredOrList(new TypeElement
                    {
                        Kind = type.Kind,
                        Name = type.Name.TrimEnd('!')
                    })
                };
            }
            else if (type.Name.EndsWith("]"))
            {
                return new TypeElement
                {
                    Kind = "LIST",
                    Name = null,
                    OfType = ConvertGqlRequiredOrList(new TypeElement
                    {
                        Kind = type.Kind,
                        Name = type.Name.TrimStart('[').TrimEnd(']')
                    })
                };
            }
            return type;
        }

        /// <summary>
        /// This is used in a lazy evaluated field as a graph can have circular dependencies
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="combinedMapping"></param>
        /// <param name="typeName"></param>
        /// <returns></returns>
        public static Models.Field[] BuildFieldsForType(ISchemaProvider schema, CombinedMapping combinedMapping, string typeName)
        {
            if (typeName == "Query")
            {
                return BuildRootQueryFields(schema, combinedMapping);
            }
            if (typeName == "Mutation")
            {
                return BuildMutationFields(schema, combinedMapping);
            }

            var fieldDescs = new List<Models.Field>();
            if (!schema.HasType(typeName))
            {
                return fieldDescs.ToArray();
            }
            var type = schema.Type(typeName);
            foreach (var field in type.GetFields())
            {
                if (field.Name.StartsWith("__"))
                    continue;

                fieldDescs.Add(new Models.Field
                {
                    Args = BuildArgs(schema, combinedMapping, field).ToArray(),
                    DeprecationReason = "",
                    Description = field.Description,
                    IsDeprecated = false,
                    Name = SchemaGenerator.ToCamelCaseStartsLower(field.Name),
                    Type = BuildType(schema, field.ReturnTypeClr, field.GetReturnType(schema), combinedMapping),
                });
            }
            return fieldDescs.ToArray();
        }

        private static Models.Field[] BuildRootQueryFields(ISchemaProvider schema, CombinedMapping combinedMapping)
        {
            var rootFields = new List<Models.Field>();

            foreach (var field in schema.GetQueryFields())
            {
                if (field.Name.StartsWith("__"))
                    continue;

                // Skipping ENUM type
                if (field.ReturnTypeClr.GetTypeInfo().IsEnum)
                    continue;

                //== Fields ==//
                rootFields.Add(new Models.Field
                {
                    Name = field.Name,
                    Args = BuildArgs(schema, combinedMapping, field).ToArray(),
                    IsDeprecated = false,
                    Type = BuildType(schema, field.ReturnTypeClr, field.GetReturnType(schema), combinedMapping),
                    Description = field.Description
                });
            }
            return rootFields.ToArray();
        }

        private static Models.Field[] BuildMutationFields(ISchemaProvider schema, CombinedMapping combinedMapping)
        {
            var rootFields = new List<Models.Field>();

            foreach (var field in schema.GetMutations())
            {
                if (field.Name.StartsWith("__"))
                    continue;

                // Skipping ENUM type
                if (field.ReturnTypeClr.GetTypeInfo().IsEnum)
                    continue;

                var args = BuildArgs(schema, combinedMapping, field).ToArray();
                rootFields.Add(new Models.Field
                {
                    Name = field.Name,
                    Args = args,
                    IsDeprecated = false,
                    Type = BuildType(schema, field.ReturnTypeClr, field.GetReturnType(schema), combinedMapping),
                    Description = field.Description
                });
            }
            return rootFields.ToArray();
        }

        private static List<InputValue> BuildArgs(ISchemaProvider schema, CombinedMapping combinedMapping, IMethodType field)
        {
            var args = new List<InputValue>();
            foreach (var arg in field.Arguments)
            {
                Type clrType = arg.Value.Type.GetNonNullableType();
                var gqlTypeName = clrType.IsEnumerableOrArray() ? clrType.GetEnumerableOrArrayType().Name : clrType.Name;
                var type = BuildType(schema, clrType, gqlTypeName, combinedMapping, true);

                args.Add(new InputValue
                {
                    Name = arg.Key,
                    Type = type,
                    DefaultValue = null,
                    Description = null,
                });
            }

            return args;
        }

        private static string FindNamedMapping(Type type, CombinedMapping combinedMapping, string fallback = null)
        {
            var mappedType = combinedMapping.GetMappedType(type);
            if (mappedType != null)
                return mappedType;

            if (string.IsNullOrEmpty(fallback))
                return type.Name;

            return fallback;
        }

        private static List<Directive> BuildDirectives(ISchemaProvider schema, CombinedMapping combinedMapping)
        {
            var directives = schema.GetDirectives().Select(directive => new Directive
            {
                Name = directive.Name,
                Description = directive.Description,
                Locations = new string[] { "FIELD", "FRAGMENT_SPREAD", "INLINE_FRAGMENT" },
                Args = directive.GetArguments().Select(arg => new InputValue
                {
                    Name = arg.Name,
                    Description = arg.Description,
                    DefaultValue = null,
                    Type = BuildType(schema, arg.Type, schema.GetSchemaTypeNameForClrType(arg.Type.GetNonNullableOrEnumerableType()), combinedMapping, true),
                }).ToArray()
            }).ToList();

            return directives;
        }

    }

    public class CombinedMapping
    {
        private Dictionary<Type, string> typeMappings;
        private Dictionary<Type, string> scalarTypes;

        public CombinedMapping(Dictionary<Type, string> typeMappings, Dictionary<Type, string> scalarTypes)
        {
            this.typeMappings = typeMappings;
            this.scalarTypes = scalarTypes;
        }
        public bool TypeIsScalar(Type clrType)
        {
            return scalarTypes.Any(x => x.Key == clrType || (clrType.GetTypeInfo().IsGenericType && clrType.GetGenericTypeDefinition() == x.Key));
        }

        public string GetMappedType(Type type)
        {
            if (scalarTypes.ContainsKey(type))
                return scalarTypes[type];
            if (typeMappings.ContainsKey(type))
                return typeMappings[type];
            return null;
        }
    }
}

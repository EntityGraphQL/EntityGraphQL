namespace EntityGraphQL.Schema
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using EntityGraphQL.Extensions;
    using EntityGraphQL.Schema.Models;

    public class SchemaIntrospection
    {
        /// <summary>
        /// Creates an Introspection schema
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="typeMappings"></param>
        /// <returns></returns>
        public static Models.Schema Make(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new List<Models.TypeElement>
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

            var schemaDescription = new Models.Schema
            {
                QueryType = new Models.TypeElement
                {
                    Name = "Query"
                },
                MutationType = new Models.TypeElement
                {
                    Name = "Mutation"
                },
                Types = types.OrderBy(x => x.Name).ToArray(),
                Directives = BuildDirectives().ToArray()
            };

            return schemaDescription;
        }

        private static IEnumerable<TypeElement> BuildScalarTypes(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new List<Models.TypeElement>();

            foreach (var customScalar in schema.CustomScalarTypes)
            {
                var typeElement = new Models.TypeElement
                {
                    Kind = "SCALAR",
                    Name = customScalar,
                    Description = null,
                };

                types.Add(typeElement);
            }

            return types;
        }

        private static List<Models.TypeElement> BuildQueryTypes(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new List<Models.TypeElement>();

            foreach (var st in schema.GetNonContextTypes().Where(s => !s.IsInput))
            {
                var typeElement = new Models.TypeElement
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
        private static List<Models.TypeElement> BuildInputTypes(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new List<Models.TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsInput))
            {
                if (schemaType.Name.StartsWith("__"))
                    continue;

                var inputValues = new List<Models.InputValue>();
                foreach (Field field in schemaType.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    //Skip any property with special attribute
                    var property = schemaType.ContextType.GetProperty(field.Name);
                    if (property != null && property.GetCustomAttribute(typeof(GraphQLIgnoreAttribute)) != null)
                        continue;

                    //Skipping custom fields added to schema
                    if (field.Resolve.NodeType == System.Linq.Expressions.ExpressionType.Call)
                        continue;

                    //Skipping ENUM type
                    if (field.ReturnTypeClr.GetTypeInfo().IsEnum)
                        continue;

                    inputValues.Add(new Models.InputValue
                    {
                        Name = field.Name,
                        Description = field.Description,
                        Type = BuildType(schema, field.ReturnTypeClr, field.ReturnTypeSingle, combinedMapping, true)
                    });
                }

                var typeElement = new Models.TypeElement
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

        private static List<Models.TypeElement> BuildEnumTypes(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new List<Models.TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes())
            {
                var typeElement = new Models.TypeElement
                {
                    Kind = "ENUM",
                    Name = string.Empty,
                    Description = null,
                    EnumValues = new Models.EnumValue[] { }
                };

                var enumTypes = new List<Models.EnumValue>();

                //filter to ENUM type ONLY!
                foreach (Field field in schemaType.GetFields().Where(x => x.ReturnTypeClr.GetTypeInfo().IsEnum))
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    typeElement.Name = field.ReturnTypeSingle;
                    typeElement.Description = field.Description;

                    foreach (var fieldInfo in field.ReturnTypeClr.GetFields())
                    {
                        if (fieldInfo.Name == "value__")
                            continue;

                        var attribute = (System.ComponentModel.DescriptionAttribute)fieldInfo.GetCustomAttribute(typeof(System.ComponentModel.DescriptionAttribute));

                        enumTypes.Add(new Models.EnumValue
                        {
                            Name = fieldInfo.Name,
                            Description = attribute?.Description,
                            IsDeprecated = false,
                            DeprecationReason = null
                        });
                    }
                }

                typeElement.EnumValues = enumTypes.ToArray();
                if (typeElement.EnumValues.Count() > 0)
                    types.Add(typeElement);
            }

            return types;
        }

        private static Models.TypeElement BuildType(ISchemaProvider schema, Type clrType, string gqlTypeName, IReadOnlyDictionary<Type, string> combinedMapping, bool isInput = false)
        {
            // Is collection of objects?
            var type = new Models.TypeElement();
            if (clrType.IsEnumerableOrArray())
            {
                type.Kind = "LIST";
                type.Name = null;
                type.OfType = BuildType(schema, clrType.GetEnumerableOrArrayType(), gqlTypeName, combinedMapping, isInput);
            }
            else if (clrType.Name == "RequiredField`1")
            {
                type.Kind = "NON_NULL";
                type.Name = null;
                type.OfType = BuildType(schema, clrType.GetGenericArguments()[0], gqlTypeName, combinedMapping, isInput);
            }
            else if (clrType.GetTypeInfo().IsEnum)
            {
                type.Kind = "ENUM";
                type.Name = FindNamedMapping(clrType, combinedMapping, gqlTypeName);
                type.OfType = null;
            }
            else
            {
                type.Kind = combinedMapping.Any(x => x.Key == clrType) ? "SCALAR" : "OBJECT";
                type.OfType = null;
                if (type.Kind == "OBJECT" && isInput)
                {
                    type.Name = SchemaGenerator.ToCamelCaseStartsLower(FindNamedMapping(clrType, combinedMapping, gqlTypeName));
                }
                else
                    type.Name = FindNamedMapping(clrType, combinedMapping, gqlTypeName);
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
        public static Models.Field[] BuildFieldsForType(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping, string typeName)
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
                    Type = BuildType(schema, field.ReturnTypeClr, field.ReturnTypeSingle, combinedMapping),
                });
            }
            return fieldDescs.ToArray();
        }

        private static Models.Field[] BuildRootQueryFields(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
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
                    Type = BuildType(schema, field.ReturnTypeClr, field.ReturnTypeSingle, combinedMapping),
                    Description = field.Description
                });
            }
            return rootFields.ToArray();
        }

        private static Models.Field[] BuildMutationFields(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
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
                    Type = BuildType(schema, field.ReturnTypeClr, field.ReturnTypeSingle, combinedMapping),
                    Description = field.Description
                });
            }
            return rootFields.ToArray();
        }

        private static List<Models.InputValue> BuildArgs(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping, IMethodType field)
        {
            var args = new List<Models.InputValue>();
            foreach (var arg in field.Arguments)
            {
                var gqlTypeName = arg.Value.IsEnumerableOrArray() ? arg.Value.GetEnumerableOrArrayType().Name : arg.Value.Name;
                var type = BuildType(schema, arg.Value, gqlTypeName, combinedMapping);

                args.Add(new Models.InputValue
                {
                    Name = arg.Key,
                    Type = type,
                    DefaultValue = null,
                    Description = null,
                });
            }

            return args;
        }

        private static string FindNamedMapping(Type name, IReadOnlyDictionary<Type, string> combinedMapping, string fallback = null)
        {
            if (combinedMapping.Any(x => x.Key == name))
                return combinedMapping[name];
            else
                if (string.IsNullOrEmpty(fallback))
                    return name.Name;
                else
                    return fallback;
        }

        private static List<Models.Directives> BuildDirectives()
        {
            var directives = new List<Models.Directives> {
                // TODO - we could have defaults in the future (currently no directives support). But likely this will be read from the dierectives users add
                // new Models.Directives
                // {
                //     Name = "include",
                //     Description = "Directs the executor to include this field or fragment only when the `if` argument is true.",
                //     Locations = new string[] { "FIELD", "FRAGMENT_SPREAD", "INLINE_FRAGMENT" },
                //     Args = new Models.Arg[] {
                //         new Models.Arg {
                //             Name = "if",
                //             Description = "Included when true.",
                //             DefaultValue = null,
                //             Type = new Models.TypeElement
                //             {
                //                 Kind = "NON_NULL",
                //                 Name = null,
                //                 OfType = new Models.TypeElement
                //                 {
                //                     Kind = "SCALAR",
                //                     Name = "Boolean",
                //                     OfType = null
                //                 }
                //             }
                //         }
                //     }
                // },
                // new Models.Directives
                // {
                //     Name = "skip",
                //     Description = "Directs the executor to skip this field or fragment when the `if` argument is true.",
                //     Locations = new string[] { "FIELD", "FRAGMENT_SPREAD", "INLINE_FRAGMENT" },
                //     Args = new Models.Arg[] {
                //         new Models.Arg {
                //             Name = "if",
                //             Description = "Skipped when true.",
                //             DefaultValue = null,
                //             Type = new Models.TypeElement
                //             {
                //                 Kind = "NON_NULL",
                //                 Name = null,
                //                 OfType = new Models.TypeElement
                //                 {
                //                     Kind = "SCALAR",
                //                     Name = "Boolean",
                //                     OfType = null
                //                 }
                //             }
                //         }
                //     }
                // }
            };

            return directives;
        }

    }
}

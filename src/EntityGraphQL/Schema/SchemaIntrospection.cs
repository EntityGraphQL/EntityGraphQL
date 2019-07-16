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
                BuildRootQuery(schema, combinedMapping),
                BuildMutationType(schema, combinedMapping)
            };
            types.AddRange(BuildQueryType(schema, combinedMapping));
            types.AddRange(BuildInputType(schema, combinedMapping));
            types.AddRange(BuildEnumType(schema, combinedMapping));

            var schemaDescription = new Models.Schema
            {
                QueryType = new Models.QueryType
                {
                    Name = "RootQuery"
                },
                MutationType = new Models.MutationType
                {
                    Name = "MutationQuery"
                },
                Types = types.OrderBy(x => x.Name).ToArray(),
                Directives = BuildDirectives().ToArray()
            };

            return schemaDescription;
        }

        private static Models.TypeElement BuildRootQuery(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var rootTypes = new Models.TypeElement
            {
                Kind = "OBJECT",
                Name = "RootQuery",
                Interfaces = new object[] { },
                Description = "Queries available on this server."
            };

            return rootTypes;
        }

        private static List<Models.TypeElement> BuildQueryType(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new List<Models.TypeElement>();

            foreach (var st in schema.GetNonContextTypes())
            {
                var fields = new List<Models.Field>();
                foreach (var field in st.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    fields.Add(new Models.Field
                    {
                        Name = field.Name,
                        Description = field.Description,
                        IsDeprecated = false,
                        Args = new Models.Arg[] { },
                        Type = BuildType(schema, field, combinedMapping)
                    });
                }

                var typeElement = new Models.TypeElement
                {
                    Kind = "OBJECT",
                    Name = st.Name,
                    Description = st.Description,
                    Interfaces = new object[] { },
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
        /// Since Types and Inputs cannot have the same name, camelCase the name to pervent duplicates.
        /// </remarks>
        /// <returns></returns>
        private static List<Models.TypeElement> BuildInputType(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new List<Models.TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsInput))
            {
                var fields = new List<Models.Field>();
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

                    fields.Add(new Models.Field
                    {
                        Name = field.Name,
                        Description = field.Description,
                        IsDeprecated = false,
                        Args = new Models.Arg[] { },
                        Type = BuildType(schema, field, combinedMapping, true)
                    });
                }

                var typeElement = new Models.TypeElement
                {
                    Kind = "INPUT_OBJECT",
                    Name = SchemaGenerator.ToCamelCaseStartsLower(schemaType.Name),
                    Description = schemaType.Description,
                    Interfaces = new object[] { },
                    InputFields = fields.ToArray()
                };

                types.Add(typeElement);
            }

            return types;
        }

        private static List<Models.TypeElement> BuildEnumType(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new List<Models.TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes())
            {
                var typeElement = new Models.TypeElement
                {
                    Kind = "ENUM",
                    Name = string.Empty,
                    Description = null,
                    Interfaces = null,
                    InputFields = new Models.Field[] { },
                    EnumValues = new Models.EnumValue[] { }
                };

                var enumTypes = new List<Models.EnumValue>();

                //filter to ENUM type ONLY!
                foreach (Field field in schemaType.GetFields().Where(x => x.ReturnTypeClr.GetTypeInfo().IsEnum))
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    ////Skipping custom fields added to schema
                    //if (field.Resolve.NodeType == System.Linq.Expressions.ExpressionType.Call)
                    //    continue;

                    typeElement.Name = field.Name;
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

        private static Models.TypeElement BuildMutationType(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var mutationTypes = new Models.TypeElement
            {
                Kind = "OBJECT",
                Name = "MutationQuery",
                Interfaces = new object[] { },
                Description = "All mutations available on this server."
            };
            var mutationFields = new List<Models.Field>();

            foreach (var mutation in schema.GetMutations())
            {
                if (mutation.Name.StartsWith("__"))
                    continue;

                /*== Arguments ==*/
                var args = new List<Models.Arg>();
                foreach (var arg in mutation.Arguments)
                {
                    //Skip any property with special attribute
                    //Had to restore the PascalCase so Reflection could find it
                    var propInfo = mutation.ReturnTypeClr.GetProperty(ToPascalCaseStartsUpper(arg.Key));
                    if (propInfo != null && propInfo.GetCustomAttribute(typeof(GraphQLIgnoreAttribute)) != null)
                        continue;

                    var type = new Models.TypeElement();
                    if (arg.Value.Namespace.Contains("System.Collections.Generic"))
                    {
                        type.Kind = "LIST";
                        type.Name = null;
                        type.OfType = new Models.TypeElement
                        {
                            Kind = "OBJECT",
                            Name = SchemaGenerator.ToCamelCaseStartsLower(arg.Value.GenericTypeArguments.First().Name),
                            OfType = null
                        };
                    }
                    else if (arg.Value.GetTypeInfo().IsEnum)
                    {
                        type.Kind = "ENUM";
                        type.Name = FindNamedMapping(arg.Value, combinedMapping, ToPascalCaseStartsUpper(arg.Key));
                        type.OfType = null;
                    }
                    else
                    {
                        type.Kind = combinedMapping.Any(x => x.Key == arg.Value) ? "SCALAR" : "OBJECT";
                        type.Name = FindNamedMapping(arg.Value, combinedMapping, arg.Key);
                    }

                    args.Add(new Models.Arg
                    {
                        Name = arg.Key,
                        Type = type
                    });
                }

                /*== Fields ==*/
                mutationFields.Add(new Models.Field
                {
                    Name = mutation.Name,
                    Description = mutation.Description,
                    Args = args.ToArray(),
                    IsDeprecated = false,
                    Type = new Models.TypeElement
                    {
                        Kind = "OBJECT",
                        Name = FindNamedMapping(mutation.ReturnTypeClr, combinedMapping, mutation.ReturnTypeClr.Name)
                    }
                });
            }

            // mutationTypes.Fields = mutationFields.ToArray();
            return mutationTypes;
        }

        private static Models.TypeElement BuildType(ISchemaProvider schema, Field field, IReadOnlyDictionary<Type, string> combinedMapping, bool isInput = false)
        {
            // Is collection of objects?
            var type = new Models.TypeElement();
            if (field.IsEnumerable)
            {
                type.Kind = "LIST";
                type.Name = null;
                type.OfType = new Models.TypeElement
                {
                    Kind = "OBJECT",
                    Name = isInput ? SchemaGenerator.ToCamelCaseStartsLower(field.ReturnTypeSingle) : field.ReturnTypeSingle
                };
            }
            else
            {
                type.Kind = combinedMapping.Any(x => x.Key == field.ReturnTypeClr) ? "SCALAR" : "OBJECT";
                if (type.Kind == "OBJECT" && isInput)
                    type.Name = SchemaGenerator.ToCamelCaseStartsLower(FindNamedMapping(field.ReturnTypeClr, combinedMapping, field.ReturnTypeSingle));
                else
                    type.Name = FindNamedMapping(field.ReturnTypeClr, combinedMapping, field.ReturnTypeSingle);
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
            if (typeName == "RootQuery")
            {
                return BuildRootQueryFields(schema, combinedMapping);
            }

            var fieldDescs = new List<Models.Field>();
            if (!schema.HasType(typeName))
            {
                return fieldDescs.ToArray();
            }
            var type = schema.Type(typeName);
            foreach (var field in type.GetFields())
            {
                fieldDescs.Add(new Models.Field
                {
                    Args = null,
                    DeprecationReason = "",
                    Description = field.Description,
                    IsDeprecated = false,
                    Name = SchemaGenerator.ToCamelCaseStartsLower(field.Name),
                    Type = BuildType(schema, field, combinedMapping),
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

                //== Arguments ==//
                var args = new List<Models.Arg>();
                foreach (var arg in field.Arguments)
                {
                    var type = new Models.TypeElement();
                    if (arg.Value.Name == "RequiredField`1")
                    {
                        type.Kind = "NON_NULL";
                        type.Name = null;
                        type.OfType = new Models.TypeElement
                        {
                            Kind = "SCALAR",
                            Name = FindNamedMapping(arg.Value, combinedMapping),
                            OfType = null
                        };
                    }
                    else
                    {
                        type.Kind = "SCALAR";
                        type.Name = FindNamedMapping(arg.Value, combinedMapping);
                        type.OfType = null;
                    }

                    args.Add(new Models.Arg
                    {
                        Name = arg.Key,
                        Type = type
                    });
                }

                //== Fields ==//
                rootFields.Add(new Models.Field
                {
                    Name = field.Name,
                    Args = args.ToArray(),
                    IsDeprecated = false,
                    Type = BuildType(schema, field, combinedMapping),
                    Description = field.Description
                });
            }
            return rootFields.ToArray();
        }

        private static string FindNamedMapping(Type name, IReadOnlyDictionary<Type, string> combinedMapping, string fallback = null)
        {
            if (combinedMapping.Any(x => x.Key == name))
                return combinedMapping[name];
            else
                if (string.IsNullOrEmpty(fallback))
                    return name.ToString();
                else
                    return fallback;
        }

        public static string ToPascalCaseStartsUpper(string name)
        {
            return name.Substring(0, 1).ToUpperInvariant() + name.Substring(1);
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

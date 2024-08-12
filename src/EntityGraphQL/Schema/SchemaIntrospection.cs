namespace EntityGraphQL.Schema
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EntityGraphQL.Directives;
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
        public static Schema Make(ISchemaProvider schema)
        {
            var types = new List<TypeElement>
            {
                new TypeElement("OBJECT", schema.QueryContextName) { Description = "The query type, represents all of the entry points into our object graph", OfType = null, },
            };
            types.AddRange(BuildQueryTypes(schema));
            types.AddRange(BuildInputTypes(schema));
            types.AddRange(BuildEnumTypes(schema));
            types.AddRange(BuildScalarTypes(schema));

            var schemaDescription = new Schema(
                new TypeElement(null, schema.QueryContextName),
                schema.HasType(schema.Mutation().SchemaType.TypeDotnet) ? new TypeElement(null, schema.Mutation().SchemaType.Name) : null,
                schema.HasType(schema.Subscription().SchemaType.TypeDotnet) ? new TypeElement(null, schema.Subscription().SchemaType.Name) : null,
                types.OrderBy(x => x.Name).ToList(),
                BuildDirectives(schema)
            );

            return schemaDescription;
        }

        private static List<TypeElement> BuildScalarTypes(ISchemaProvider schema)
        {
            var types = new List<TypeElement>();

            foreach (var customScalar in schema.GetScalarTypes())
            {
                var typeElement = new TypeElement("SCALAR", customScalar.Name) { Description = customScalar.Description };

                customScalar.Directives.ProcessType(typeElement);

                types.Add(typeElement);
            }

            return types;
        }

        private static List<TypeElement> BuildQueryTypes(ISchemaProvider schema)
        {
            var types = new List<TypeElement>();

            foreach (var st in schema.GetNonContextTypes().Where(s => !s.IsInput && !s.IsEnum && !s.IsScalar))
            {
                var kind = st.GqlType switch
                {
                    GqlTypes.Interface => "INTERFACE",
                    GqlTypes.Union => "UNION",
                    _ => "OBJECT"
                };

                var typeElement = new TypeElement(kind, st.Name)
                {
                    Description = st.Description,
                    PossibleTypes = st.PossibleTypesReadOnly.Select(i => new TypeElement("OBJECT", i.Name))?.ToArray() ?? Array.Empty<TypeElement>()
                };

                if (st.BaseTypesReadOnly != null && st.BaseTypesReadOnly.Count > 0)
                {
                    typeElement.Interfaces = st.BaseTypesReadOnly.Select(baseType => new TypeElement("INTERFACE", baseType.Name)).ToArray();
                }

                types.Add(typeElement);
            }

            return types;
        }

        /// <summary>
        /// Build INPUT Type to be used by Mutations
        /// </summary>
        /// <param name="schema"></param>
        /// <remarks>
        /// Since Types and Inputs cannot have the same name, camelCase the name to prevent duplicates.
        /// </remarks>
        /// <returns></returns>
        private static List<TypeElement> BuildInputTypes(ISchemaProvider schema)
        {
            var types = new List<TypeElement>();

            foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsInput))
            {
                if (schemaType.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                var inputValues = new List<InputValue>();
                foreach (var field in schemaType.GetFields().Cast<Field>())
                {
                    if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                        continue;

                    // Skip any property with special attribute
                    var property = schemaType.TypeDotnet.GetProperty(field.Name);
                    if (property != null && GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(property))
                        continue;

                    // Skipping custom fields added to schema
                    if (field.ResolveExpression?.NodeType == System.Linq.Expressions.ExpressionType.Call)
                        continue;

                    // Skipping ENUM type
                    if (field.ReturnType.TypeDotnet.IsEnum)
                        continue;

                    inputValues.Add(new InputValue(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet, true)) { Description = field.Description, });
                }

                var typeElement = new TypeElement("INPUT_OBJECT", schemaType.Name) { Description = schemaType.Description, InputFields = inputValues.ToArray() };

                schemaType.Directives.ProcessType(typeElement);

                types.Add(typeElement);
            }

            return types;
        }

        private static List<TypeElement> BuildEnumTypes(ISchemaProvider schema)
        {
            var types = new List<TypeElement>();

            // filter to ENUM type ONLY!
            foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsEnum))
            {
                var typeElement = new TypeElement("ENUM", schemaType.Name) { Description = schemaType.Description, EnumValues = Array.Empty<EnumValue>() };
                if (schemaType.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                var enumTypes = new List<EnumValue>();

                foreach (var field in schemaType.GetFields().Cast<Field>())
                {
                    if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                        continue;

                    var e = new EnumValue(field.Name) { Description = field.Description, };

                    field.DirectivesReadOnly.ProcessEnumValue(e);

                    enumTypes.Add(e);
                }

                typeElement.EnumValues = enumTypes.ToArray();
                if (typeElement.EnumValues.Length > 0)
                    types.Add(typeElement);
            }

            return types;
        }

        private static TypeElement BuildType(ISchemaProvider schema, GqlTypeInfo typeInfo, Type clrType, bool isInput = false)
        {
            // Is collection of objects?
            var type = new TypeElement();
            if (clrType.IsEnumerableOrArray())
            {
                type.Kind = "LIST";
                type.Name = null;
                type.OfType = BuildType(schema, typeInfo, typeInfo.SchemaType.TypeDotnet, isInput);
            }
            else if (clrType.Name == "EntityQueryType`1")
            {
                type.Kind = "SCALAR";
                type.Name = "String";
                type.OfType = null;
            }
            else if (clrType.IsEnum)
            {
                type.Kind = "ENUM";
                type.Name = typeInfo.SchemaType.Name;
                type.OfType = null;
            }
            else
            {
                type.Kind = typeInfo.SchemaType.IsScalar ? "SCALAR" : "OBJECT";
                type.OfType = null;
                if (type.Kind == "OBJECT" && isInput)
                {
                    type.Kind = "INPUT_OBJECT";
                }
                type.Name = typeInfo.SchemaType.Name;
            }
            if (typeInfo.TypeNotNullable)
            {
                return new TypeElement("NON_NULL", null) { OfType = type };
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
        public static Models.Field[] BuildFieldsForType(ISchemaProvider schema, string typeName)
        {
            if (typeName == schema.QueryContextName)
            {
                return BuildRootQueryFields(schema);
            }
            if (typeName == schema.Mutation().SchemaType.Name)
            {
                return BuildMutationFields(schema);
            }

            var fieldDescs = new List<Models.Field>();
            if (!schema.HasType(typeName))
            {
                return fieldDescs.ToArray();
            }
            var type = schema.Type(typeName);
            foreach (var field in type.GetFields())
            {
                if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                var f = new Models.Field(schema.SchemaFieldNamer(field.Name), BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet))
                {
                    Args = BuildArgs(schema, field).ToArray(),
                    Description = field.Description,
                };

                field.DirectivesReadOnly.ProcessField(f);

                fieldDescs.Add(f);
            }
            return fieldDescs.ToArray();
        }

        private static Models.Field[] BuildRootQueryFields(ISchemaProvider schema)
        {
            var rootFields = new List<Models.Field>();

            foreach (var field in schema.Type(schema.QueryContextName).GetFields())
            {
                if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                // Skipping ENUM type
                if (field.ReturnType.TypeDotnet.IsEnum)
                    continue;

                //== Fields ==//
                var f = new Models.Field(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet)) { Args = BuildArgs(schema, field).ToArray(), Description = field.Description };

                field.DirectivesReadOnly.ProcessField(f);

                rootFields.Add(f);
            }
            return rootFields.ToArray();
        }

        private static Models.Field[] BuildMutationFields(ISchemaProvider schema)
        {
            var rootFields = new List<Models.Field>();

            foreach (var field in schema.GetSchemaType(schema.MutationType, false, null).GetFields())
            {
                if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                var args = BuildArgs(schema, field).ToArray();
                var f = new Models.Field(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet)) { Args = args, Description = field.Description };

                field.DirectivesReadOnly.ProcessField(f);

                rootFields.Add(f);
            }
            return rootFields.ToArray();
        }

        private static List<InputValue> BuildArgs(ISchemaProvider schema, IField field)
        {
            var args = new List<InputValue>();
            if (field.ArgumentsAreInternal)
                return args;

            foreach (var arg in field.Arguments)
            {
                var type = BuildType(schema, arg.Value.Type, arg.Value.Type.TypeDotnet, true);

                var stringValue = SchemaGenerator.GetArgDefaultValue(arg.Value.DefaultValue, schema.SchemaFieldNamer)?.Trim('"');
                var defaultValue = string.IsNullOrEmpty(stringValue) ? null : stringValue;

                args.Add(new InputValue(arg.Key, type) { DefaultValue = defaultValue, Description = arg.Value.Description, });
            }

            return args;
        }

        private static List<Directive> BuildDirectives(ISchemaProvider schema)
        {
            var directives = schema
                .GetDirectives()
                .Select(directive => new Directive(directive.Name)
                {
                    Description = directive.Description,
                    Locations = directive.Location.Select(i => Enum.GetName(typeof(ExecutableDirectiveLocation), i))!,
                    Args = directive
                        .GetArguments(schema)
                        .Values.Select(arg => new InputValue(arg.Name, BuildType(schema, arg.Type, arg.Type.TypeDotnet, true)) { Description = arg.Description, DefaultValue = null, })
                        .ToArray()
                })
                .ToList();

            return directives;
        }
    }
}

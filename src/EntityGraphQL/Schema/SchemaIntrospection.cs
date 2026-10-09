using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.Models;

namespace EntityGraphQL.Schema;

public static class SchemaIntrospection
{
    /// <summary>
    /// Creates an Introspection schema. When a requestContext is supplied the result only includes the types and fields
    /// the requesting user may see (see <see cref="SchemaVisibility"/>) - the same set ToGraphQLSchemaString(requestContext)
    /// outputs as SDL. Without a request context nothing is filtered.
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="requestContext">The executing request's context (user) used to filter protected types/fields. Null does no filtering</param>
    /// <returns></returns>
    public static Models.Schema Make(ISchemaProvider schema, QueryRequestContext? requestContext = null)
    {
        var visibility = SchemaVisibility.For(schema, requestContext);
        var types = new List<TypeElement>
        {
            new("OBJECT", schema.QueryContextName) { Description = "The query type, represents all of the entry points into our object graph", OfType = null },
        };
        types.AddRange(BuildQueryTypes(schema, visibility));
        types.AddRange(BuildInputTypes(schema, visibility));
        types.AddRange(BuildEnumTypes(schema, visibility));
        types.AddRange(BuildScalarTypes(schema, visibility));

        // a root none of whose fields the user may see is not advertised, as in the SDL
        bool HasRoot(ISchemaType rootType) => schema.HasType(rootType.TypeDotnet) && (!visibility.IsFiltering || rootType.GetFields().Any(f => !IsInternal(f) && visibility.IsFieldVisible(f)));
        var mutationRoot = schema.Mutation().SchemaType;
        var subscriptionRoot = schema.Subscription().SchemaType;
        if (visibility.IsFiltering)
            types.RemoveAll(t => (t.Name == mutationRoot.Name && !HasRoot(mutationRoot)) || (t.Name == subscriptionRoot.Name && !HasRoot(subscriptionRoot)));

        var schemaDescription = new Models.Schema(
            new TypeElement("OBJECT", schema.QueryContextName),
            HasRoot(mutationRoot) ? new TypeElement("OBJECT", mutationRoot.Name) : null,
            HasRoot(subscriptionRoot) ? new TypeElement("OBJECT", subscriptionRoot.Name) : null,
            types.OrderBy(x => x.Name).ToList(),
            BuildDirectives(schema)
        );

        return schemaDescription;
    }

    private static bool IsInternal(IField field) => field.Name.StartsWith("__", StringComparison.InvariantCulture);

    private static List<TypeElement> BuildScalarTypes(ISchemaProvider schema, SchemaVisibility visibility)
    {
        var types = new List<TypeElement>();

        foreach (var customScalar in schema.GetScalarTypes().Where(visibility.IsTypeVisible))
        {
            var typeElement = new TypeElement("SCALAR", customScalar.Name) { Description = customScalar.Description };

            customScalar.Directives.ProcessType(typeElement);

            types.Add(typeElement);
        }

        return types;
    }

    private static List<TypeElement> BuildQueryTypes(ISchemaProvider schema, SchemaVisibility visibility)
    {
        var types = new List<TypeElement>();

        foreach (var st in schema.GetNonContextTypes().Where(s => !s.IsInput && !s.IsEnum && !s.IsScalar))
        {
            if (!visibility.IsTypeVisible(st))
                continue;

            var kind = st.GqlType switch
            {
                GqlTypes.Interface => "INTERFACE",
                GqlTypes.Union => "UNION",
                _ => "OBJECT",
            };

            var typeElement = new TypeElement(kind, st.Name)
            {
                Description = st.Description,
                PossibleTypes = st.PossibleTypesReadOnly.Where(visibility.IsTypeVisible).Select(i => new TypeElement("OBJECT", i.Name))?.ToArray() ?? Array.Empty<TypeElement>(),
            };

            if (st.BaseTypesReadOnly != null && st.BaseTypesReadOnly.Count > 0)
            {
                typeElement.Interfaces = st.BaseTypesReadOnly.Where(visibility.IsTypeVisible).Select(baseType => new TypeElement("INTERFACE", baseType.Name)).ToArray();
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
    private static List<TypeElement> BuildInputTypes(ISchemaProvider schema, SchemaVisibility visibility)
    {
        var types = new List<TypeElement>();

        foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsInput))
        {
            if (schemaType.Name.StartsWith("__", StringComparison.InvariantCulture))
                continue;

            if (!visibility.IsTypeVisible(schemaType))
                continue;

            var inputValues = new List<InputValue>();
            foreach (var field in schemaType.GetFields().Cast<Field>())
            {
                if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                if (!visibility.IsFieldVisible(field))
                    continue;

                // Skip any property with special attribute
                var property = schemaType.TypeDotnet.GetProperty(field.Name);
                if (property != null && GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(property))
                    continue;

                // Skipping custom fields added to schema
                if (field.ResolveExpression?.NodeType == System.Linq.Expressions.ExpressionType.Call)
                    continue;

                var inputValue = new InputValue(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet, true)) { Description = field.Description };
                field.DirectivesReadOnly.ProcessInputValue(inputValue);
                inputValues.Add(inputValue);
            }

            // per spec isOneOf is non-null for input object types - the @oneOf directive sets it true below
            var typeElement = new TypeElement("INPUT_OBJECT", schemaType.Name)
            {
                Description = schemaType.Description,
                InputFields = inputValues.ToArray(),
                IsOneOf = false,
            };

            schemaType.Directives.ProcessType(typeElement);

            types.Add(typeElement);
        }

        return types;
    }

    private static List<TypeElement> BuildEnumTypes(ISchemaProvider schema, SchemaVisibility visibility)
    {
        var types = new List<TypeElement>();

        // filter to ENUM type ONLY!
        foreach (ISchemaType schemaType in schema.GetNonContextTypes().Where(s => s.IsEnum))
        {
            var typeElement = new TypeElement("ENUM", schemaType.Name) { Description = schemaType.Description, EnumValues = [] };
            if (schemaType.Name.StartsWith("__", StringComparison.InvariantCulture))
                continue;

            if (!visibility.IsTypeVisible(schemaType))
                continue;

            var enumTypes = new List<EnumValue>();

            foreach (var field in schemaType.GetFields().Cast<Field>())
            {
                if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                var e = new EnumValue(field.Name) { Description = field.Description };

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
    /// This is used in a lazy evaluated field as a graph can have circular dependencies.
    /// Per GraphQL spec, fields should only be returned for OBJECT and INTERFACE types.
    /// Returns null for INPUT_OBJECT, ENUM, SCALAR, and UNION types.
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="typeName"></param>
    /// <param name="typeKind">The GraphQL type kind (OBJECT, INTERFACE, INPUT_OBJECT, etc.)</param>
    /// <param name="includeDeprecated">Whether to include deprecated fields</param>
    /// <returns></returns>
    public static IEnumerable<Models.Field>? BuildFieldsForType(ISchemaProvider schema, string typeName, string? typeKind, bool includeDeprecated, QueryRequestContext? requestContext = null)
    {
        // Per GraphQL spec, fields should only be returned for OBJECT and INTERFACE types
        if (typeKind != "OBJECT" && typeKind != "INTERFACE")
        {
            return null;
        }

        var visibility = SchemaVisibility.For(schema, requestContext);
        Models.Field[] fields;
        if (typeName == schema.QueryContextName)
        {
            fields = BuildRootQueryFields(schema, visibility);
        }
        else if (typeName == schema.Mutation().SchemaType.Name)
        {
            fields = BuildMutationFields(schema, visibility);
        }
        else
        {
            var fieldDescs = new List<Models.Field>();
            if (!schema.HasType(typeName))
            {
                return fieldDescs;
            }
            var type = schema.Type(typeName);
            if (!visibility.IsTypeVisible(type))
                return fieldDescs;
            foreach (var field in type.GetFields())
            {
                if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                if (!visibility.IsFieldVisible(field))
                    continue;

                var f = new Models.Field(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet)) { Args = BuildArgs(schema, field).ToArray(), Description = field.Description };

                field.DirectivesReadOnly.ProcessField(f);

                fieldDescs.Add(f);
            }
            fields = fieldDescs.ToArray();
        }

        if (includeDeprecated)
            return fields;
        return fields.Where(f => !f.IsDeprecated);
    }

    private static Models.Field[] BuildRootQueryFields(ISchemaProvider schema, SchemaVisibility visibility)
    {
        var rootFields = new List<Models.Field>();

        foreach (var field in schema.Type(schema.QueryContextName).GetFields())
        {
            if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                continue;

            // Skipping ENUM type
            if (field.ReturnType.TypeDotnet.IsEnum)
                continue;

            if (!visibility.IsFieldVisible(field))
                continue;

            //== Fields ==//
            var f = new Models.Field(field.Name, BuildType(schema, field.ReturnType, field.ReturnType.TypeDotnet)) { Args = BuildArgs(schema, field).ToArray(), Description = field.Description };

            field.DirectivesReadOnly.ProcessField(f);

            rootFields.Add(f);
        }
        return rootFields.ToArray();
    }

    private static Models.Field[] BuildMutationFields(ISchemaProvider schema, SchemaVisibility visibility)
    {
        var rootFields = new List<Models.Field>();

        foreach (var field in schema.GetSchemaType(schema.MutationType, false, null).GetFields())
        {
            if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                continue;

            if (!visibility.IsFieldVisible(field))
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

            var stringValue = SchemaGenerator.GetArgDefaultValue(arg.Value.DefaultValue, schema.SchemaFieldNamer);
            var defaultValue = string.IsNullOrEmpty(stringValue) ? null : stringValue;

            args.Add(
                new InputValue(arg.Key, type)
                {
                    DefaultValue = defaultValue,
                    Description = arg.Value.Description,
                    IsDeprecated = arg.Value.IsDeprecated,
                    DeprecationReason = arg.Value.DeprecationReason,
                }
            );
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
                IsRepeatable = directive.IsRepeatable,
                Locations = directive.Location.Select(i => i.GetDescription())!,
                Args = directive
                    .GetArguments(schema)
                    .Values.Select(arg => new InputValue(arg.Name, BuildType(schema, arg.Type, arg.Type.TypeDotnet, true)) { Description = arg.Description, DefaultValue = null })
                    .ToArray(),
            })
            .ToList();

        return directives;
    }
}

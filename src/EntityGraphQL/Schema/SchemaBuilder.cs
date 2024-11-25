using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using Humanizer;
using Microsoft.Extensions.Logging;
using Nullability;

namespace EntityGraphQL.Schema;

/// <summary>
/// A simple schema provider to automatically create a query schema based on an object.
/// Commonly used with a DbContext.
/// </summary>
public static class SchemaBuilder
{
    /// <summary>
    /// Apply any options not passed via the constructor
    /// </summary>
    private static SchemaProvider<TContext> ApplyOptions<TContext>(SchemaProvider<TContext> schema, SchemaBuilderSchemaOptions options)
    {
        schema.AllowedExceptions.AddRange(options.AllowedExceptions);
        return schema;
    }

    /// <summary>
    /// Create a new SchemaProvider<TContext> with the query context of type TContext and using the SchemaBuilderSchemaOptions supplied or the default if null.
    /// Note the schema is empty, you need to add types and fields.
    /// </summary>
    /// <typeparam name="TContext">Query context type</typeparam>
    /// <param name="options">SchemaBuilderSchemaOptions to configure the options of the schema provider created</param>
    /// <param name="logger">A logger to use in the schema</param>
    /// <returns></returns>
    public static SchemaProvider<TContext> Create<TContext>(SchemaBuilderSchemaOptions? options = null, ILogger<SchemaProvider<TContext>>? logger = null)
    {
        options ??= new SchemaBuilderSchemaOptions();
        var schema = new SchemaProvider<TContext>(options.AuthorizationService, options.FieldNamer, logger, options.IntrospectionEnabled, options.IsDevelopment);
        return ApplyOptions(schema, options);
    }

    /// <summary>
    /// Given the type TContextType recursively create a query schema based on the public properties of the object.
    /// </summary>
    /// <param name="buildOptions">SchemaBuilderOptions to use to create the SchemaProvider and configure the rules for auto creating the schema types and fields</param>
    /// <param name="logger">A logger to use in the schema</param>
    /// <typeparam name="TContextType"></typeparam>
    /// <returns></returns>
    public static SchemaProvider<TContextType> FromObject<TContextType>(SchemaBuilderOptions? buildOptions = null, ILogger<SchemaProvider<TContextType>>? logger = null)
    {
        buildOptions ??= new SchemaBuilderOptions();
        var schemaOptions = new SchemaBuilderSchemaOptions();

        var schema = new SchemaProvider<TContextType>(schemaOptions.AuthorizationService, schemaOptions.FieldNamer, logger, schemaOptions.IntrospectionEnabled, schemaOptions.IsDevelopment);
        schema = ApplyOptions(schema, schemaOptions);
        return FromObject(schema, buildOptions);
    }

    /// <summary>
    /// Given the type TContextType recursively create a query schema based on the public properties of the object.
    /// </summary>
    /// <param name="schemaOptions">Options to create the SchemaProvider.</param>
    /// <param name="buildOptions">SchemaBuilderOptions to use to create the SchemaProvider and configure the rules for auto creating the schema types and fields</param>
    /// <param name="logger">A logger to use in the schema</param>
    /// <typeparam name="TContextType"></typeparam>
    /// <returns></returns>
    public static SchemaProvider<TContextType> FromObject<TContextType>(
        SchemaBuilderSchemaOptions? schemaOptions,
        SchemaBuilderOptions? buildOptions = null,
        ILogger<SchemaProvider<TContextType>>? logger = null
    )
    {
        buildOptions ??= new SchemaBuilderOptions();
        schemaOptions ??= new SchemaBuilderSchemaOptions();

        var schema = new SchemaProvider<TContextType>(schemaOptions.AuthorizationService, schemaOptions.FieldNamer, logger, schemaOptions.IntrospectionEnabled, schemaOptions.IsDevelopment);
        schemaOptions.PreBuildSchemaFromContext?.Invoke(schema);
        schema = ApplyOptions(schema, schemaOptions);
        return FromObject(schema, buildOptions);
    }

    /// <summary>
    /// Given the type TContextType recursively create a query schema based on the public properties of the object. Schema is added into the provider schema
    /// </summary>
    /// <param name="schema">Schema to add types to.</param>
    /// <param name="options">SchemaBuilderOptions to use to create the SchemaProvider and configure the rules for auto creating the schema types and fields</param>
    /// <typeparam name="TContextType"></typeparam>
    /// <returns></returns>
    internal static SchemaProvider<TContextType> FromObject<TContextType>(SchemaProvider<TContextType> schema, SchemaBuilderOptions options)
    {
        var contextType = typeof(TContextType);
        var rootFields = GetFieldsFromObject(contextType, schema.Query(), schema, options, false);
        foreach (var f in rootFields)
        {
            schema.Query().AddField(f);
        }
        return schema;
    }

    private static Field? MakeFieldWithIdArgumentIfExists(ISchemaProvider schema, ISchemaType schemaType, Type contextType, Field fieldProp, SchemaBuilderOptions options)
    {
        if (fieldProp.ResolveExpression == null)
            throw new ArgumentException($"Field '{fieldProp.Name}' does not have a resolve function. This is required for AutoCreateIdArguments to work.");
        if (!fieldProp.ResolveExpression.Type.IsEnumerableOrArray())
            return null;
        var returnSchemaType = fieldProp.ReturnType.SchemaType;
        var idFieldDef = returnSchemaType.GetFields().FirstOrDefault(f => f.Name == "id");
        if (idFieldDef == null)
            return null;

        if (idFieldDef.ResolveExpression == null)
            throw new ArgumentException($"Field '{idFieldDef.Name}' does not have a resolve function. This is required for AutoCreateIdArguments to work.");

        // We need to build an anonymous type with id = RequiredField<idFieldDef.Resolve.Type>()
        // Resulting lambda is (a, p) => a.Where(b => b.Id == p.Id).First()
        // This allows us to "insert" .Select() (and .Include()) before the .First()
        var requiredFieldType = typeof(RequiredField<>).MakeGenericType(idFieldDef.ResolveExpression.Type);
        var fieldNameAndType = new Dictionary<string, Type> { { "id", requiredFieldType } };
        var argTypes = LinqRuntimeTypeBuilder.GetDynamicType(fieldNameAndType, fieldProp.Name);
        var argTypesValue = Activator.CreateInstance(argTypes);
        var argTypeParam = Expression.Parameter(argTypes, $"args_{argTypes.Name}");
        Type arrayContextType = returnSchemaType.TypeDotnet;
        var arrayContextParam = Expression.Parameter(arrayContextType, $"arrcxt_{arrayContextType.Name}");
        var ctxId = Expression.PropertyOrField(arrayContextParam, "Id");
        Expression argId = Expression.PropertyOrField(argTypeParam, "id");
        argId = Expression.Property(argId, "Value"); // call RequiredField<>.Value to get the real type without a convert
        var idBody = Expression.MakeBinary(ExpressionType.Equal, ctxId, argId);
        var idLambda = Expression.Lambda(idBody, new[] { arrayContextParam });
        Expression body = ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.Where), [arrayContextType], fieldProp.ResolveExpression, idLambda);

        body = ExpressionUtil.MakeCallOnQueryable(nameof(Queryable.FirstOrDefault), [arrayContextType], body);
        var contextParam = Expression.Parameter(contextType, $"cxt_{contextType.Name}");
        var lambdaParams = new[] { contextParam, argTypeParam };
        body = new ParameterReplacer().Replace(body, fieldProp.FieldParam!, contextParam);
        var selectionExpression = Expression.Lambda(body, lambdaParams);
        var name = fieldProp.Name.Singularize();
        if (name == null || name == fieldProp.Name)
        {
            // If we can't singularize it (or it returns the same name) just use the name plus something as GraphQL doesn't support field overloads
            name = $"{fieldProp.Name}ById";
        }
        var f = new Field(
            schema,
            schemaType,
            name,
            selectionExpression,
            $"Return a {fieldProp.ReturnType.SchemaType.Name} by its Id",
            argTypesValue,
            new GqlTypeInfo(fieldProp.ReturnType.SchemaTypeGetter, selectionExpression.Body.Type),
            fieldProp.RequiredAuthorization
        );
        options.OnFieldCreated?.Invoke(f);
        return f;
    }

    public static List<BaseField> GetFieldsFromObject(Type type, ISchemaType fromType, ISchemaProvider schema, SchemaBuilderOptions options, bool isInputType)
    {
        var fields = new List<BaseField>();
        // cache fields/properties
        var param = Expression.Parameter(type, $"p_{type.Name}");
        if (type.IsArray || type.IsEnumerableOrArray())
            return fields;

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var f = ProcessFieldOrPropertyIntoField(fromType, prop, param, schema, options, isInputType)?.ToList();
            if (f != null)
                fields.AddRange(f);
        }
        foreach (var prop in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var f = ProcessFieldOrPropertyIntoField(fromType, prop, param, schema, options, isInputType)?.ToList();
            if (f != null)
                fields.AddRange(f);
        }
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static))
        {
            var attribute = method.GetCustomAttribute<GraphQLFieldAttribute>();
            if (attribute == null)
                continue;

            var f = ProcessMethodIntoField(fromType, method, param, schema, options, isInputType);

            if (f != null)
                fields.Add(f);
        }
        return fields;
    }

    internal static BaseField? ProcessMethodIntoField(ISchemaType fromType, MethodInfo method, ParameterExpression param, ISchemaProvider schema, SchemaBuilderOptions options, bool isInputType)
    {
        if (!ShouldIncludeMember(method, options, isInputType))
            return null;

        (string name, string description) = GetNameAndDescription(method, schema);

        options ??= new SchemaBuilderOptions();
        var isAsync = method.GetCustomAttribute<AsyncStateMachineAttribute>() != null || (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
        var requiredClaims = schema.AuthorizationService.GetRequiredAuthFromMember(method);

        LambdaExpression? le = null;
        // Fields for the schema (no services to [GraphQLArguments] that get expanded)
        Dictionary<string, ArgType>? fieldSchemaArgs = null;
        Dictionary<string, ParameterExpression> fieldServices = [];
        Dictionary<string, Type> typesFlattenToSchema = [];
        Type? fieldArgType = null;

        if (method.GetParameters().Length > 0)
        {
            // This needs args, services and the [GraphQLArguments] types (not the expanded ones on the schema)
            var argsForCallExpression = new Dictionary<string, Expression>();
            // Arg type for the created type that holds all the method args
            // this is current how query field args work. They expect 1 type that has all the schema args as props
            var fieldDotnetArgtypes = new Dictionary<string, Type>();
            fieldSchemaArgs = [];
            // typesFlattenToSchema is the dotnet types that were flattened to the GQL schema args from [GraphQLArguments]
            var argumentsFromMethod = GetGraphQlSchemaArgumentsFromMethod(schema, method, options, out typesFlattenToSchema);
            // figure out the arg type
            foreach (var item in argumentsFromMethod)
            {
                if (item.IsService)
                    continue;
                if (item.ShouldFlatten)
                {
                    foreach (var arg in item.FlattenArgs!)
                    {
                        fieldSchemaArgs.Add(arg.ArgName, arg.ArgType!);
                        fieldDotnetArgtypes.Add(arg.ArgName, arg.ArgType!.RawType);
                    }
                }
                else
                {
                    fieldSchemaArgs.Add(item.ArgName, item.ArgType!);
                    fieldDotnetArgtypes.Add(item.ArgName, item.ArgType!.RawType);
                }
            }
            fieldArgType = LinqRuntimeTypeBuilder.GetDynamicType(fieldDotnetArgtypes, method.Name)!;
            var argTypeParam = Expression.Parameter(fieldArgType, $"args_{fieldArgType.Name}");
            // build argument expressions for the call
            foreach (var item in argumentsFromMethod)
            {
                if (item.IsService)
                {
                    fieldServices.Add(item.ArgName, Expression.Parameter(item.ServiceType!, item.ArgName));
                    argsForCallExpression.Add(item.ArgName, fieldServices[item.ArgName]!);
                }
                else if (item.ShouldFlatten)
                {
                    // need to create expression new ArgType { Prop1 = args.Prop1, Prop2 = args.Prop2 }
                    var propExpressions = new Dictionary<string, Expression>();
                    foreach (var arg in item.FlattenArgs!)
                    {
                        propExpressions.Add(arg.ArgType!.DotnetName, Expression.PropertyOrField(argTypeParam!, arg.ArgName));
                    }
                    var newExpArg =
                        ExpressionUtil.CreateNewExpression(propExpressions, item.FlattenType!, true)
                        ?? throw new EntityQuerySchemaException($"Could not create expression for argument {item.ArgName} of type {item.ArgType!.RawType.Name}");
                    argsForCallExpression.Add(item.ArgName, newExpArg);
                }
                else
                {
                    argsForCallExpression.Add(item.ArgName, Expression.PropertyOrField(argTypeParam!, item.ArgName));
                }
            }

            var call = Expression.Call(method.IsStatic ? null : param, method, argsForCallExpression.Values);
            var lambdaParams = new[] { param, argTypeParam }.Concat(fieldServices.Values);
            le = Expression.Lambda(call, lambdaParams);
        }
        else
        {
            var call = Expression.Call(param, method);
            le = Expression.Lambda(call, param);
        }

        var baseReturnType = GetBaseReturnType(schema, le.ReturnType, options);

        if (options.IgnoreTypes.Contains(baseReturnType))
            return null;

        var schemaType = CacheType(baseReturnType, schema, options, false);

        var nullabilityInfo = method.GetNullabilityInfo();
        var returnTypeInfo = schema.GetCustomTypeMapping(le.ReturnType) ?? new GqlTypeInfo(() => schemaType ?? schema.GetSchemaType(baseReturnType, isInputType, null), le.Body.Type, nullabilityInfo);
        var field = new Field(schema, fromType, name, le, description, fieldSchemaArgs, returnTypeInfo, requiredClaims);
        options.OnFieldCreated?.Invoke(field);

        if (fieldServices.Count > 0)
        {
            field.Services = fieldServices.Values.ToList();
            field.ExpressionArgumentType = fieldArgType;
        }

        field.ApplyAttributes(method.GetCustomAttributes());

        return field;
    }

    private static bool ShouldIncludeMember(MemberInfo prop, SchemaBuilderOptions options, bool isInputType)
    {
        if (options.IgnoreProps.Contains(prop.Name) || GraphQLIgnoreAttribute.ShouldIgnoreMemberFromQuery(prop))
            return false;

        if (isInputType && GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(prop))
            return false;

        if (prop is PropertyInfo propertyInfo && propertyInfo.GetIndexParameters().Length > 0)
        {
            return false;
        }

        foreach (var attribute in prop.GetCustomAttributes())
        {
            if (options.IgnoreAttributes.Contains(attribute.GetType()))
                return false;
        }

        return true;
    }

    private static IEnumerable<Field>? ProcessFieldOrPropertyIntoField(
        ISchemaType fromType,
        MemberInfo prop,
        ParameterExpression param,
        ISchemaProvider schema,
        SchemaBuilderOptions options,
        bool isInputType
    )
    {
        if (!ShouldIncludeMember(prop, options, isInputType))
            yield break;

        (string name, string description) = GetNameAndDescription(prop, schema);

        LambdaExpression? le = null;
        le = prop.MemberType switch
        {
            MemberTypes.Property => Expression.Lambda(Expression.Property(param, prop.Name), param),
            MemberTypes.Field => Expression.Lambda(Expression.Field(param, prop.Name), param),
            _ => throw new NotImplementedException($"{nameof(ProcessFieldOrPropertyIntoField)} unknown MemberType: {prop.MemberType}"),
        };
        var requiredClaims = schema.AuthorizationService.GetRequiredAuthFromMember(prop);

        var baseReturnType = GetBaseReturnType(schema, le.ReturnType, options);

        if (options.IgnoreTypes.Contains(baseReturnType))
            yield break;

        var schemaType = CacheType(baseReturnType, schema, options, isInputType);

        var nullabilityInfo = prop.GetNullabilityInfo();
        // see if there is a direct type mapping from the expression return to to something.
        // otherwise build the type info
        var returnTypeInfo = schema.GetCustomTypeMapping(le.ReturnType) ?? new GqlTypeInfo(() => schemaType ?? schema.GetSchemaType(baseReturnType, isInputType, null), le.Body.Type, nullabilityInfo);
        var field = new Field(schema, fromType, name, le, description, null, returnTypeInfo, requiredClaims);
        options.OnFieldCreated?.Invoke(field);

        if (options.AutoCreateFieldWithIdArguments && (!schema.HasType(prop.DeclaringType!) || schema.GetSchemaType(prop.DeclaringType!, isInputType, null).GqlType != GqlTypes.InputObject))
        {
            // add non-plural field with argument of ID
            var idArgField = MakeFieldWithIdArgumentIfExists(schema, fromType, prop.ReflectedType!, field, options);
            if (idArgField != null)
            {
                yield return idArgField;
            }
        }

        field.ApplyAttributes(prop.GetCustomAttributes());

        yield return field;
    }

    private static Type GetBaseReturnType(ISchemaProvider schema, Type returnType, SchemaBuilderOptions options)
    {
        // get the object type returned (ignoring list etc) so we know the context to find fields etc
        var returnsTask = returnType.GetCustomAttribute<AsyncStateMachineAttribute>() != null || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));
        if (returnsTask || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)))
        {
            returnType = returnType.GetGenericArguments()[0];
        }
        if (returnType.IsDictionary())
        {
            // check for dictionaries
            if (options.AutoCreateNewComplexTypes)
            {
                Type[] genericTypeArguments = returnType.GenericTypeArguments;
                returnType = typeof(KeyValuePair<,>).MakeGenericType(genericTypeArguments);
                if (!schema.HasType(returnType))
                    schema.AddScalarType(
                        returnType,
                        $"{genericTypeArguments[0].Name}{genericTypeArguments[1].Name}KeyValuePair",
                        $"Key value pair of {genericTypeArguments[0].Name} & {genericTypeArguments[1].Name}"
                    );
            }
        }
        else
            returnType = returnType.IsEnumerableOrArray() ? returnType.GetEnumerableOrArrayType()! : returnType.GetNonNullableType()!;

        Type baseReturnType = returnType;
        if (baseReturnType.IsEnumerableOrArray())
            baseReturnType = baseReturnType.GetEnumerableOrArrayType()!;
        return baseReturnType;
    }

    internal static (string name, string description) GetNameAndDescription(MemberInfo prop, ISchemaProvider schema)
    {
        var name = schema.SchemaFieldNamer(prop.Name);
        var description = string.Empty;
        var descAttribute = prop.GetCustomAttribute<DescriptionAttribute>(false);
        if (descAttribute != null)
        {
            description = descAttribute.Description;
        }

        var attribute = prop.GetCustomAttribute<GraphQLFieldAttribute>();
        if (attribute != null)
        {
            if (!string.IsNullOrEmpty(attribute.Name))
                name = attribute.Name;

            if (!string.IsNullOrEmpty(attribute.Description))
                description = attribute.Description;
        }
        return (name, description);
    }

    internal static ISchemaType? CacheType(Type propType, ISchemaProvider schema, SchemaBuilderOptions options, bool isInputType)
    {
        if (!schema.HasType(propType))
        {
            var typeInfo = propType;
            string description = string.Empty;
            var d = typeInfo.GetCustomAttribute<DescriptionAttribute>(false);
            if (d != null)
            {
                description = d.Description;
            }

            var typeName = BuildTypeName(propType);

            if ((options.AutoCreateNewComplexTypes && typeInfo.IsClass) || ((typeInfo.IsInterface || typeInfo.IsAbstract) && options.AutoCreateInterfaceTypes))
            {
                var fieldCount = typeInfo.GetProperties().Length + typeInfo.GetFields().Length;

                // add type before we recurse more that may also add the type
                // dynamically call generic method
                // hate this, but want to build the types with the right generics so you can extend them later.
                // this is not the fastest, but only done on schema creation
                var addMethod = (isInputType, typeInfo.IsInterface, typeInfo.IsAbstract, fieldCount) switch
                {
                    (true, _, _, _) => nameof(ISchemaProvider.AddInputType),
                    (_, true, _, > 0) => nameof(ISchemaProvider.AddInterface),
                    (_, _, true, > 0) => nameof(ISchemaProvider.AddInterface),
                    (_, true, _, _) => nameof(ISchemaProvider.AddUnion),
                    (_, _, true, _) => nameof(ISchemaProvider.AddUnion),
                    _ => nameof(ISchemaProvider.AddType),
                };

                var method = schema.GetType().GetMethod(addMethod, [typeof(string), typeof(string)]) ?? throw new EntityQuerySchemaException($"Could not find {addMethod} method on schema");
                method = method.MakeGenericMethod(propType);
                var typeAdded = (ISchemaType)method.Invoke(schema, new object[] { typeName, description })!;
                typeAdded.RequiredAuthorization = schema.AuthorizationService.GetRequiredAuthFromType(propType);

                var fields = GetFieldsFromObject(propType, typeAdded, schema, options, isInputType);
                typeAdded.AddFields(fields);

                if (options.AutoCreateInterfaceTypes && !typeAdded.IsInput)
                {
                    typeAdded.ImplementAllBaseTypes(true, true);
                }
                return typeAdded;
            }
            else if (options.AutoCreateEnumTypes && typeInfo.IsEnum && !schema.HasType(typeName))
            {
                return schema.AddEnum(propType.Name, propType, description);
            }
            else if (options.AutoCreateEnumTypes && propType.IsNullableType() && Nullable.GetUnderlyingType(propType)!.IsEnum && !schema.HasType(Nullable.GetUnderlyingType(propType)!.Name))
            {
                Type type = Nullable.GetUnderlyingType(propType)!;
                return schema.AddEnum(type.Name, type, description);
            }
        }
        if (schema.TryGetSchemaType(propType, isInputType, out var schemaType, null))
        {
            return schemaType;
        }
        return null;
    }

    internal static string BuildTypeName(Type propType)
    {
        return propType.IsGenericType ? $"{propType.Name[..propType.Name.IndexOf('`')]}{string.Join("", propType.GetGenericArguments().Select(BuildTypeName))}" : propType.Name;
    }

    public static GqlTypeInfo MakeGraphQlType(ISchemaProvider schema, bool isInputType, Type returnType, string? returnSchemaType, string fieldName, ISchemaType fromType)
    {
        Func<ISchemaType> typeGetter = !string.IsNullOrEmpty(returnSchemaType)
            ? () => schema.Type(returnSchemaType) // We can look the type up by it's unique schema name
            // we need to look it up by the dotnet type
            : () =>
            {
                var getType = returnType.IsEnumerableOrArray() || returnType.IsNullableType() ? returnType.GetNonNullableOrEnumerableType() : returnType;
                if (schema.TryGetSchemaType(getType, isInputType, out var schemaType, null))
                    return schemaType!;
                throw new EntityGraphQLCompilerException(
                    $"No schema type found for dotnet type '{getType.Name}'. Make sure you add it or add a type mapping. Lookup failed for field '{fieldName}' on type '{fromType.Name}'"
                );
            };
        return new GqlTypeInfo(typeGetter, returnType);
    }

    public static IEnumerable<FieldArgInfo> GetGraphQlSchemaArgumentsFromMethod(
        ISchemaProvider schema,
        MethodInfo method,
        SchemaBuilderOptions options,
        out Dictionary<string, Type> flattenArgumentTypes
    )
    {
        flattenArgumentTypes = [];
        var arguments = new List<FieldArgInfo>();
        foreach (var item in method.GetParameters())
        {
            if (GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(item))
                continue;

            var inputType = item.ParameterType.IsEnumerableOrArray() ? item.ParameterType.GetEnumerableOrArrayType()! : item.ParameterType;
            if (inputType.IsNullableType())
                inputType = inputType.GetGenericArguments()[0];

            if (inputType == schema.QueryContextType)
            {
                arguments.Add(new FieldArgInfo(item.Name!, item.ParameterType));
                continue;
            }

            // primitive types are arguments or types already known in the schema
            var shouldBeAddedAsArg =
                item.ParameterType.IsPrimitive || (schema.HasType(inputType) && (schema.Type(inputType).IsInput || schema.Type(inputType).IsScalar || schema.Type(inputType).IsEnum));

            // if hasServices then we expect attributes
            var argumentsAttr = item.GetCustomAttribute<GraphQLArgumentsAttribute>() ?? item.ParameterType.GetTypeInfo().GetCustomAttribute<GraphQLArgumentsAttribute>();
            var inlineArgumentAttr = item.GetCustomAttribute<GraphQLInputTypeAttribute>() ?? item.ParameterType.GetTypeInfo().GetCustomAttribute<GraphQLInputTypeAttribute>();
            if (!shouldBeAddedAsArg && argumentsAttr == null && inlineArgumentAttr == null)
            {
                arguments.Add(new FieldArgInfo(item.Name!, item.ParameterType));
                continue;
            }

            if (argumentsAttr != null)
            {
                arguments.Add(new FieldArgInfo(item.Name!, item.ParameterType, FlattenArguments(item.ParameterType, schema, options)));
                flattenArgumentTypes.Add(item.Name!, item.ParameterType);
            }
            else
            {
                arguments.Add(new FieldArgInfo(schema.SchemaFieldNamer(item.Name!), ArgType.FromParameter(schema, item, item.HasDefaultValue ? item.DefaultValue : null)));

                if (!schema.HasType(inputType) && options.AutoCreateInputTypes)
                {
                    // use input type as it has resolved to a non list/nullable type
                    CacheType(inputType, schema, options, true);
                }
            }
        }
        return arguments;
    }

    private static IEnumerable<FieldArgInfo> FlattenArguments(Type argType, ISchemaProvider schema, SchemaBuilderOptions options)
    {
        foreach (var item in argType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(item))
                continue;

            yield return new FieldArgInfo(schema.SchemaFieldNamer(item.Name), ArgType.FromProperty(schema, item, null));
            var inputType =
                item.PropertyType.IsEnumerableOrArray() ? item.PropertyType.GetEnumerableOrArrayType()!
                : item.PropertyType.IsNullableType() ? item.PropertyType.GetGenericArguments()[0]
                : item.PropertyType;
            if (!schema.HasType(inputType) && options.AutoCreateInputTypes)
            {
                CacheType(inputType, schema, options, true);
            }
        }
        foreach (var item in argType.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(item))
                continue;
            yield return new FieldArgInfo(schema.SchemaFieldNamer(item.Name), ArgType.FromField(schema, item, null));
            var inputType =
                item.FieldType.IsEnumerableOrArray() ? item.FieldType.GetEnumerableOrArrayType()!
                : item.FieldType.IsNullableType() ? item.FieldType.GetGenericArguments()[0]
                : item.FieldType;
            if (!schema.HasType(inputType) && options.AutoCreateInputTypes)
            {
                CacheType(inputType, schema, options, true);
            }
        }
    }
}

public class FieldArgInfo
{
    public FieldArgInfo(string argName, Type flattenType, IEnumerable<FieldArgInfo> flattedArgs)
    {
        ArgName = argName;
        FlattenType = flattenType;
        FlattenArgs = flattedArgs;
    }

    public FieldArgInfo(string argName, ArgType argType)
    {
        ArgName = argName;
        ArgType = argType;
    }

    public FieldArgInfo(string argName, Type serviceType)
    {
        ArgName = argName;
        ServiceType = serviceType;
    }

    public string ArgName { get; }
    public Type? FlattenType { get; }
    public ArgType? ArgType { get; }

    /// <summary>
    /// This argument is a service
    /// </summary>
    public bool IsService => ServiceType != null;
    public Type? ServiceType { get; }

    /// <summary>
    /// This argument should be flatten in the GraphQL schema. But not in the method call
    /// </summary>
    public bool ShouldFlatten => FlattenArgs != null;
    public IEnumerable<FieldArgInfo>? FlattenArgs { get; }
}

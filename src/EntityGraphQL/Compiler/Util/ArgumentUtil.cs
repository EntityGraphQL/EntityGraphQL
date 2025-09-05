using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public static class ArgumentUtil
{
    public static object? BuildArgumentsObject(
        ISchemaProvider schema,
        string fieldName,
        IField? field,
        IReadOnlyDictionary<string, object?> args,
        IEnumerable<ArgType> argumentDefinitions,
        Type? argumentsType,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        List<string> validationErrors,
        CompileContext? compileContext = null
    )
    {
        if (argumentsType == null)
        {
            return new();
        }

        // get the values for the argument anonymous type object constructor
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        // if a variable was set or is just the default dotnet value
        var setValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // if they used AddField("field", new { id = Required<int>() }) the compiler makes properties and a constructor with the values passed in
        foreach (var argField in argumentDefinitions)
        {
            object? val;
            try
            {
                if (args.ContainsKey(argField.Name) && args[argField.Name] is Expression argExpression)
                {
                    // this value comes from the variables from the query document
                    if (docVariables != null)
                    {
                        val = Expression.Lambda(argExpression, docParam!).Compile().DynamicInvoke([docVariables]);
                        // leave it out if it was not set - will default to dotnet default but the `IsSet will not carry through
                        if (docVariables.IsSet(((MemberExpression)argExpression).Member.Name))
                            setValues.Add(argField.Name);
                    }
                    else
                    {
                        val = argExpression;
                        setValues.Add(argField.Name);
                    }
                    values.Add(argField.Name, ExpressionUtil.ConvertObjectType(val, argField.RawType, schema));
                }
                else
                {
                    (var isSet, val) = BuildArgumentFromMember(schema, args, argField.Name, argField.RawType, argField.DefaultValue, validationErrors, compileContext);
                    // this could be int to RequiredField<int>
                    if (val != null && val.GetType() != argField.RawType)
                        val = ExpressionUtil.ConvertObjectType(val, argField.RawType, schema);
                    values.Add(argField.Name, val);
                    if (val != null || argField.DefaultValue.IsSet)
                        setValues.Add(argField.Name);
                }
                if (field != null)
                    argField.ValidateAsync(val, field, validationErrors).GetAwaiter().GetResult();
            }
            catch (EntityGraphQLValidationException)
            {
                throw;
            }
            catch (Exception)
            {
                validationErrors.Add($"Variable or value used for argument '{argField.Name}' does not match argument type '{argField.Type}' on field '{fieldName}'");
            }
        }

        // Build our object
        var con = argumentsType!.GetConstructors()?.FirstOrDefault() ?? throw new EntityGraphQLCompilerException($"Could not find constructor for arguments type {argumentsType.Name}");
        var parameters = con.GetParameters().Select(x => values!.GetValueOrDefault(x.Name)).ToArray();

        // anonymous objects will have a constructor with taking the properties as arguments
        var argumentValues = con.Invoke(parameters);

        // regardless of the constructor, we make sure the values are set on the object
        var argMembers = argumentValues.GetType().GetMembers();
        bool isPropTracking = typeof(IArgumentsTracker).IsAssignableFrom(argumentValues.GetType());

        foreach (var item in argMembers)
        {
            if (item.MemberType != MemberTypes.Property && item.MemberType != MemberTypes.Field)
                continue;
            var type = item.MemberType == MemberTypes.Property ? ((PropertyInfo)item).PropertyType : ((FieldInfo)item).FieldType;
            var argsAttribute = type.GetCustomAttribute<GraphQLArgumentsAttribute>();
            if (!values.ContainsKey(item.Name) && argsAttribute == null)
                continue;

            SetArgumentValues(values, argumentValues, item);

            if (isPropTracking && setValues.Contains(item.Name))
            {
                ((IArgumentsTracker)argumentValues).MarkAsSet(item.Name);
            }
        }

        if (field != null)
        {
            var validators = argumentsType!.GetCustomAttributes<ArgumentValidatorAttribute>();
            if (validators != null)
            {
                var context = new ArgumentValidatorContext(field, argumentValues);
                foreach (var validator in validators)
                {
                    validator.Validator.ValidateAsync(context).GetAwaiter().GetResult();
                    argumentValues = context.Arguments;

                    validationErrors.AddRange(context.Errors);
                }
            }
        }

        return argumentValues;
    }

    private static void SetArgumentValues(Dictionary<string, object?> values, object argumentValues, MemberInfo item)
    {
        if (item is FieldInfo fieldInfo)
        {
            fieldInfo.SetValue(argumentValues, values[item.Name]);
        }
        else if (item is PropertyInfo propertyInfo)
        {
            if (propertyInfo.CanWrite)
            {
                propertyInfo.SetValue(argumentValues, values[item.Name]);
            }
        }
    }

    internal static (bool isSet, object? value) BuildArgumentFromMember(
        ISchemaProvider schema,
        IReadOnlyDictionary<string, object?>? args,
        string memberName,
        Type memberType,
        DefaultArgValue defaultValue,
        IList<string> validationErrors,
        CompileContext? compileContext
    )
    {
        string argName = memberName;
        // check we have required arguments
        if (memberType.GetGenericArguments().Length > 0 && memberType.GetGenericTypeDefinition() == typeof(RequiredField<>))
        {
            // Error is created by caller on arg validation
            if (args == null || !args.ContainsKey(argName))
            {
                return (false, null);
            }
            var item = args[argName];
            if (item is null)
            {
                return (false, null);
            }
            var constructor = memberType.GetConstructor([item.GetType()]);
            if (constructor == null)
            {
                // we might need to change the type
                foreach (var c in memberType.GetConstructors())
                {
                    var parameters = c.GetParameters();
                    if (parameters.Length == 1)
                    {
                        item = ExpressionUtil.ConvertObjectType(item, parameters[0].ParameterType, schema);
                        constructor = memberType.GetConstructor(new[] { item!.GetType() });
                        break;
                    }
                }
            }

            if (constructor == null)
            {
                validationErrors.Add($"Could not find a constructor for type {memberType.Name} that takes value '{item}'");
                return (false, null);
            }

            var typedVal = constructor.Invoke([item]);
            return (true, typedVal);
        }
        else if (defaultValue.IsSet && defaultValue.Value != null && defaultValue.GetType() == typeof(EntityQueryType))
        {
            return (true, args != null && args.ContainsKey(argName) ? args[argName] : Activator.CreateInstance(memberType));
        }
        else if (args != null && args.ContainsKey(argName))
        {
            return (true, args[argName]);
        }
        else
        {
            // set the default value
            return (defaultValue.IsSet, defaultValue.Value);
        }
    }
}

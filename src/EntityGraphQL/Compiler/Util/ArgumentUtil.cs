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
    public static object? BuildArgumentsObject(ISchemaProvider schema, string fieldName, IField? field, IReadOnlyDictionary<string, object> args, IEnumerable<ArgType> argumentDefinitions, Type? argumentsType, ParameterExpression? docParam, object? docVariables, List<string> validationErrors)
    {
        // get the values for the argument anonymous type object constructor
        var propVals = new Dictionary<PropertyInfo, object?>();
        var fieldVals = new Dictionary<FieldInfo, object?>();

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
                        val = Expression.Lambda(argExpression, docParam!).Compile().DynamicInvoke(new[] { docVariables });
                    else
                        val = argExpression;
                    if (argField.MemberInfo is PropertyInfo info)
                        propVals.Add(info!, ExpressionUtil.ChangeType(val, ((PropertyInfo)argField.MemberInfo!).PropertyType, schema));
                    else
                        fieldVals.Add((FieldInfo)argField.MemberInfo!, ExpressionUtil.ChangeType(val, ((FieldInfo)argField.MemberInfo!).FieldType, schema));
                }
                else
                {
                    val = BuildArgumentFromMember(schema, args, argField.Name, argField.RawType, argField.DefaultValue, validationErrors);
                    // this could be int to RequiredField<int>
                    if (val != null && val.GetType() != argField.RawType)
                        val = ExpressionUtil.ChangeType(val, argField.RawType, schema);
                    if (argField.MemberInfo != null)
                    {
                        if (argField.MemberInfo is PropertyInfo info)
                            propVals.Add(info, val);
                        else
                            fieldVals.Add((FieldInfo)argField.MemberInfo, val);
                    }
                }
                argField.Validate(val, fieldName, validationErrors);
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

        // create a copy of the anonymous object. It will have the default values set
        // there is only 1 constructor for the anonymous type that takes all the property values
        var con = argumentsType!.GetConstructor(propVals.Keys.Select(v => v.PropertyType).ToArray());
        object? argumentValues;
        if (con != null)
        {
            argumentValues = con.Invoke(propVals.Values.ToArray());
            foreach (var item in fieldVals)
            {
                item.Key.SetValue(argumentValues, item.Value);
            }
        }
        else
        {
            // expect an empty constructor
            con = argumentsType.GetConstructor(Array.Empty<Type>())!;
            argumentValues = con.Invoke(Array.Empty<object>());
            foreach (var item in fieldVals)
            {
                item.Key.SetValue(argumentValues, item.Value);
            }
            foreach (var item in propVals)
            {
                item.Key.SetValue(argumentValues, item.Value);
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

    internal static object? BuildArgumentFromMember(ISchemaProvider schema, IReadOnlyDictionary<string, object>? args, string memberName, Type memberType, object? defaultValue, IList<string> validationErrors)
    {
        string argName = memberName;
        // check we have required arguments
        if (memberType.GetGenericArguments().Any() && memberType.GetGenericTypeDefinition() == typeof(RequiredField<>))
        {
            // Error is created by caller on arg validation
            if (args == null || !args.ContainsKey(argName))
            {
                return null;
            }
            var item = args[argName];
            var constructor = memberType.GetConstructor(new[] { item.GetType() });
            if (constructor == null)
            {
                // we might need to change the type
                foreach (var c in memberType.GetConstructors())
                {
                    var parameters = c.GetParameters();
                    if (parameters.Length == 1)
                    {
                        item = ExpressionUtil.ChangeType(item, parameters[0].ParameterType, schema);
                        constructor = memberType.GetConstructor(new[] { item!.GetType() });
                        break;
                    }
                }
            }

            if (constructor == null)
            {
                validationErrors.Add($"Could not find a constructor for type {memberType.Name} that takes value '{item}'");
                return null;
            }

            var typedVal = constructor.Invoke(new[] { item });
            return typedVal;
        }
        else if (defaultValue != null && defaultValue.GetType().IsConstructedGenericType && defaultValue.GetType().GetGenericTypeDefinition() == typeof(EntityQueryType<>))
        {
            return args != null && args.ContainsKey(argName) ? args[argName] : Activator.CreateInstance(memberType);
        }
        else if (args != null && args.ContainsKey(argName))
        {
            return args[argName];
        }
        else
        {
            // set the default value
            return defaultValue;
        }
    }
}
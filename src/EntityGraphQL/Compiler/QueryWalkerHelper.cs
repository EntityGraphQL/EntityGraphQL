using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using HotChocolate.Language;

namespace EntityGraphQL.Compiler
{
    public static class QueryWalkerHelper
    {
        public static readonly Regex GuidRegex = new(@"^[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}$", RegexOptions.IgnoreCase);

        public static object ProcessArgumentOrVariable(ISchemaProvider schema, QueryVariables variables, ArgumentNode argument, Type argType)
        {
            var argName = argument.Name.Value;
            if (argument.Value.Kind == SyntaxKind.Variable)
            {
                string varKey = ((VariableNode)argument.Value).Name.Value;
                if (variables == null)
                    throw new EntityGraphQLCompilerException($"Missing variable {varKey}");
                object value = variables.GetValueFor(varKey);
                return ConvertArgIfRequired(value, argType, argName);
            }
            return ProcessArgumentValue(schema, argument.Value, argName, argType);
        }

        public static object ProcessArgumentValue(ISchemaProvider schema, IValueNode argumentValue, string argName, Type argType)
        {
            object argValue = null;
            switch (argumentValue.Kind)
            {
                case SyntaxKind.IntValue:
                    argValue = long.Parse(argumentValue.Value.ToString());
                    break;
                // these ones are the correct type
                case SyntaxKind.StringValue:
                    argValue = (string)argumentValue.Value;
                    break;
                case SyntaxKind.BooleanValue:
                    argValue = argumentValue.Value;
                    break;
                case SyntaxKind.NullValue:
                    argValue = null;
                    break;
                case SyntaxKind.EnumValue:
                    argValue = (string)argumentValue.Value;
                    break;
                case SyntaxKind.ListValue:
                    argValue = ProcessListArgument(schema, (List<IValueNode>)argumentValue.Value, argName, argType);
                    break;
                case SyntaxKind.ObjectValue:
                    {
                        // this should be an Input type
                        var obj = Activator.CreateInstance(argType);
                        var schemaType = schema.Type(argType);
                        foreach (var item in (List<ObjectFieldNode>)argumentValue.Value)
                        {
                            if (!schemaType.HasField(item.Name.Value))
                                throw new EntityGraphQLCompilerException($"Field {item.Name.Value} not found of type {schemaType.Name}");
                            var schemaField = schemaType.GetField(item.Name.Value, null);

                            var nameFromType = ((MemberExpression)schemaField.Resolve).Member.Name;
                            var prop = argType.GetProperty(nameFromType);

                            if (prop == null)
                            {
                                var field = argType.GetField(nameFromType);
                                if (field == null)
                                    throw new EntityGraphQLCompilerException($"Field {item.Name.Value} not found on object argument");
                                field.SetValue(obj, ProcessArgumentValue(schema, item.Value, argName, field.FieldType));
                            }
                            else
                            {
                                prop.SetValue(obj, ProcessArgumentValue(schema, item.Value, argName, prop.PropertyType));
                            }
                        }
                        argValue = obj;
                    }
                    break;
                case SyntaxKind.FloatValue:
                    argValue = double.Parse(argumentValue.Value.ToString());
                    break;
            }

            return ConvertArgIfRequired(argValue, argType, argName);
        }

        private static object ConvertArgIfRequired(object argValue, Type argType, string argName)
        {
            argType = argType.GetNonNullableType();

            if ((argType == typeof(Guid) || argType == typeof(Guid?) ||
                argType == typeof(RequiredField<Guid>) || argType == typeof(RequiredField<Guid?>)) &&
                argValue.GetType() == typeof(string) && GuidRegex.IsMatch(argValue.ToString()))
            {
                return Guid.Parse(argValue.ToString());
            }

            if (argType.IsEnum)
            {
                var enumType = argType.GetNonNullableType();
                var argStr = argValue.ToString();
                var valueIndex = Enum.GetNames(enumType).ToList().FindIndex(n => n == argStr);
                if (valueIndex == -1)
                    throw new EntityGraphQLCompilerException($"Value {argStr} is not valid for argument {argName}");

                var enumValue = Enum.GetValues(enumType).GetValue(valueIndex);
                return enumValue;
            }

            return argValue;
        }

        public static object ProcessListArgument(ISchemaProvider schema, List<IValueNode> values, string argName, Type fieldArgType)
        {
            var list = (IList)Activator.CreateInstance(fieldArgType);

            foreach (var item in values)
            {
                list.Add(ProcessArgumentValue(schema, item, argName, fieldArgType));
            }

            return list;
        }

        public static Type GetDotnetType(ISchemaProvider schema, string value)
        {
            var schemaType = schema.GetSchemaType(value);
            return schemaType.TypeDotnet;
        }

        public static void ProcessVariableDefinitions(ISchemaProvider schemaProvider, QueryVariables variables, OperationDefinitionNode node)
        {
            foreach (var item in node.VariableDefinitions)
            {
                var argName = item.Variable.Name.Value;
                if (item.DefaultValue != null)
                {
                    var varType = variables.ContainsKey(argName) ? variables[argName].GetType() : GetDotnetType(schemaProvider, ((NamedTypeNode)item.Type).Name.Value);
                    variables[argName] = Expression.Lambda(Expression.Constant(ProcessArgumentValue(schemaProvider, item.DefaultValue, argName, varType))).Compile().DynamicInvoke();
                }

                var required = item.Type.Kind == SyntaxKind.NonNullType;
                if (required && !variables.ContainsKey(argName))
                {
                    throw new QueryException($"Missing required variable '{argName}' on operation '{node.Name.Value}'");
                }
            }
        }
    }
}
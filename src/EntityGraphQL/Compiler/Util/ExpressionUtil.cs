using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.Util
{
    public static class ExpressionUtil
    {
        public static Expression MakeCallOnQueryable(string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            var type = typeof(IQueryable).IsAssignableFrom(parameters.First().Type) ? typeof(Queryable) : typeof(Enumerable);
            try
            {
                return Expression.Call(type, methodName, genericTypes, parameters);
            }
            catch (InvalidOperationException ex)
            {
                throw new EntityGraphQLCompilerException($"Could not find extension method {methodName} on types {type}", ex);
            }
        }

        public static Expression MakeCallOnEnumerable(string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            var type = typeof(Enumerable);
            try
            {
                return Expression.Call(type, methodName, genericTypes, parameters);
            }
            catch (InvalidOperationException ex)
            {
                throw new EntityGraphQLCompilerException($"Could not find extension method {methodName} on types {type}", ex);
            }
        }

        public static MemberExpression CheckAndGetMemberExpression<TBaseType, TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection)
        {
            var exp = fieldSelection.Body;
            if (exp.NodeType == ExpressionType.Convert)
                exp = ((UnaryExpression)exp).Operand;

            if (exp.NodeType != ExpressionType.MemberAccess)
                throw new ArgumentException("fieldSelection should be a property or field accessor expression only. E.g (t) => t.MyField", "fieldSelection");
            return (MemberExpression)exp;
        }

        public static object? ChangeType(object? value, Type toType)
        {
            if (value == null)
                return null;

            var fromType = value.GetType();
            // Default JSON deserializer will deserialize child objects in QueryVariables as this JSON type
            if (typeof(JsonElement).IsAssignableFrom(fromType))
            {
                var jsonEle = (JsonElement)value;
                if (jsonEle.ValueKind == JsonValueKind.Object)
                {
                    value = Activator.CreateInstance(toType);
                    foreach (var item in jsonEle.EnumerateObject())
                    {
                        var prop = toType.GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == item.Name.ToLowerInvariant());
                        if (prop != null)
                            prop.SetValue(value, ChangeType(item.Value, prop.PropertyType));
                        else
                        {
                            var field = toType.GetFields().FirstOrDefault(p => p.Name.ToLowerInvariant() == item.Name.ToLowerInvariant());
                            if (field != null)
                                field.SetValue(value, ChangeType(item.Value, field.FieldType));
                        }
                    }
                    return value;
                }
                if (jsonEle.ValueKind == JsonValueKind.Array)
                {
                    var eleType = toType.GetEnumerableOrArrayType()!;
                    var list = (IList?)Activator.CreateInstance(typeof(List<>).MakeGenericType(eleType));
                    if (list == null)
                        throw new EntityGraphQLCompilerException($"Could not create list of type {eleType}");
                    foreach (var item in jsonEle.EnumerateArray())
                        list.Add(ChangeType(item, eleType));
                    return list;
                }
                value = jsonEle.ToString();
                fromType = value.GetType();

                if (value == null)
                    return null;
            }

            if (toType != typeof(string) && fromType == typeof(string))
            {
                if (toType == typeof(double) || toType == typeof(double?))
                    return double.Parse((string)value);
                if (toType == typeof(float) || toType == typeof(float?))
                    return float.Parse((string)value);
                if (toType == typeof(int) || toType == typeof(int?))
                    return int.Parse((string)value);
                if (toType == typeof(uint) || toType == typeof(uint?))
                    return uint.Parse((string)value);
                if (toType == typeof(DateTime) || toType == typeof(DateTime?))
                    return DateTime.Parse((string)value);
                if (toType == typeof(DateTimeOffset) || toType == typeof(DateTimeOffset?))
                    return DateTimeOffset.Parse((string)value);
            }
            else if (toType != typeof(long) && fromType == typeof(long))
            {
                if (toType == typeof(DateTime) || toType == typeof(DateTime?))
                    return new DateTime((long)value!);
                if (toType == typeof(DateTimeOffset) || toType == typeof(DateTimeOffset?))
                    return new DateTimeOffset((long)value!, TimeSpan.Zero);
            }

            var argumentNonNullType = toType.IsNullableType() ? Nullable.GetUnderlyingType(toType)! : toType;
            var valueNonNullType = fromType.IsNullableType() ? Nullable.GetUnderlyingType(fromType) : fromType;
            if (argumentNonNullType.IsEnum)
            {
                return valueNonNullType == typeof(string) ? Enum.Parse(argumentNonNullType, (string)value) : Enum.ToObject(argumentNonNullType, value);
            }
            if (fromType.IsDictionary())
            {
                // handle dictionary of dictionary representing the objects
                if (fromType.GetGenericArguments()[0] != typeof(string))
                    throw new EntityGraphQLCompilerException($"Dictionary key type must be string. Got {fromType.GetGenericArguments()[0]}");

                var newValue = Activator.CreateInstance(toType);
                foreach (string key in ((IDictionary<string, object>)value).Keys)
                {
                    var toProp = toType.GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == key.ToLowerInvariant());
                    if (toProp != null)
                        toProp.SetValue(newValue, ChangeType(((IDictionary)value)[key], toProp.PropertyType));
                    else
                    {
                        var toField = toType.GetFields().FirstOrDefault(p => p.Name.ToLowerInvariant() == key.ToLowerInvariant());
                        if (toField != null)
                            toField.SetValue(newValue, ChangeType(((IDictionary)value)[key], toField.FieldType));
                    }
                }
                return newValue;
            }
            if (toType.IsEnumerableOrArray())
            {
                var eleType = toType.GetEnumerableOrArrayType()!;
                var list = (IList?)Activator.CreateInstance(typeof(List<>).MakeGenericType(eleType));
                if (list == null)
                    throw new EntityGraphQLCompilerException($"Could not create list of type {eleType}");
                foreach (var item in (IEnumerable)value)
                    list.Add(ChangeType(item, eleType));
                return list;
            }
            if (argumentNonNullType.IsClass && typeof(string) != argumentNonNullType)
            {
                return ConvertObjectType(value, toType, fromType);
            }
            if ((argumentNonNullType == typeof(Guid) || argumentNonNullType == typeof(Guid?) ||
                argumentNonNullType == typeof(RequiredField<Guid>) || argumentNonNullType == typeof(RequiredField<Guid?>)) &&
                fromType == typeof(string) && QueryWalkerHelper.GuidRegex.IsMatch(value?.ToString()))
            {
                return Guid.Parse(value!.ToString());
            }
            if (argumentNonNullType != valueNonNullType)
            {
                var newVal = Convert.ChangeType(value, argumentNonNullType);
                return newVal;
            }
            return value;
        }

        public static object? ConvertObjectType(object? value, Type toType, Type valueObjType)
        {
            var newValue = Activator.CreateInstance(toType);
            foreach (var toField in toType.GetFields())
            {
                var fromProp = valueObjType.GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == toField.Name.ToLowerInvariant());
                if (fromProp != null)
                    toField.SetValue(newValue, ChangeType(fromProp.GetValue(value), toField.FieldType));
                else
                {
                    var fromField = valueObjType.GetFields().FirstOrDefault(p => p.Name.ToLowerInvariant() == toField.Name.ToLowerInvariant());
                    if (fromField != null)
                        toField.SetValue(newValue, ChangeType(fromField.GetValue(value), toField.FieldType));
                }
            }
            foreach (var toProperty in toType.GetProperties())
            {
                var fromProp = valueObjType.GetProperties().FirstOrDefault(p => p.Name.ToLowerInvariant() == toProperty.Name.ToLowerInvariant());
                if (fromProp != null)
                    toProperty.SetValue(newValue, ChangeType(fromProp.GetValue(value), toProperty.PropertyType));
                else
                {
                    var fromField = valueObjType.GetFields().FirstOrDefault(p => p.Name.ToLowerInvariant() == toProperty.Name.ToLowerInvariant());
                    if (fromField != null)
                        toProperty.SetValue(newValue, ChangeType(fromField.GetValue(value), toProperty.PropertyType));
                }
            }
            return newValue;
        }

        public static Dictionary<string, ArgType> ObjectToDictionaryArgs(ISchemaProvider schema, object argTypes, Func<string, string> fieldNamer)
        {
            var args = argTypes.GetType().GetProperties().Where(p => !GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(p)).ToDictionary(k => fieldNamer(k.Name), p => ArgType.FromProperty(schema, p, p.GetValue(argTypes), fieldNamer));
            argTypes.GetType().GetFields().Where(p => !GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(p)).ToList().ForEach(p => args.Add(fieldNamer(p.Name), ArgType.FromField(schema, p, p.GetValue(argTypes), fieldNamer)));
            return args;
        }

        public static Type MergeTypes(Type? type1, Type type2)
        {
            if (type1 == null)
                return type2;

            if (type2 == null)
                throw new ArgumentNullException("type2");

            var fields = type1.GetFields().ToDictionary(f => f.Name, f => f.FieldType);
            type1.GetProperties().ToList().ForEach(f => fields.Add(f.Name, f.PropertyType));
            type2.GetFields().ToList().ForEach(f => fields.Add(f.Name, f.FieldType));
            type2.GetProperties().ToList().ForEach(f => fields.Add(f.Name, f.PropertyType));

            var newType = LinqRuntimeTypeBuilder.GetDynamicType(fields);
            return newType;
        }

        /// <summary>
        /// If the combineExpression is a First()/etc. with a filter, pull the filter back to a Where() in the collection field expression
        /// </summary>
        /// <param name="collectionSelectionNode"></param>
        /// <param name="combineExpression"></param>
        public static string? UpdateCollectionNodeFieldExpression(GraphQLListSelectionField collectionSelectionNode, Expression combineExpression)
        {
            string? capMethod = null;
            if (combineExpression.NodeType == ExpressionType.Call)
            {
                // In the case of a First() we need to insert that select before the first
                // This is all to have 1 nice expression that can work with ORMs (like EF)
                // E.g  we want db => db.Entity.Select(e => new {name = e.Name, ...}).First(filter)
                // we dot not want db => new {name = db.Entity.First(filter).Name, ...})

                var call = (MethodCallExpression)combineExpression;
                if (call.Method.Name == "First" || call.Method.Name == "FirstOrDefault" ||
                    call.Method.Name == "Last" || call.Method.Name == "LastOrDefault" ||
                    call.Method.Name == "Single" || call.Method.Name == "SingleOrDefault")
                {
                    // Get the expression that we can add the Select() too
                    var contextExpression = collectionSelectionNode.ListExpression;
                    if (contextExpression != null && call.Arguments.Count == 2)
                    {
                        // this is a ctx.Something.First(f => ...)
                        // move the filter to a Where call so we can use .Select() to get the fields requested
                        var filter = call.Arguments.ElementAt(1);
                        var isQueryable = typeof(IQueryable).IsAssignableFrom(contextExpression.Type);
                        contextExpression = isQueryable ?
                            MakeCallOnQueryable("Where", new Type[] { combineExpression.Type }, contextExpression, filter) :
                            MakeCallOnEnumerable("Where", new Type[] { combineExpression.Type }, contextExpression, filter);
                        // we can first call ToList() as the data is filtered so risk of over fetching is low
                        capMethod = call.Method.Name;
                        collectionSelectionNode.ListExpression = contextExpression;
                    }
                }
            }
            return capMethod;
        }

        /// <summary>
        /// Tries to take 2 expressions returned from FindIEnumerable and join them together. I.e. If we Split list.First() with FindIEnumerable, we can join it back together with newList.First()
        /// </summary>
        /// <param name="baseExp"></param>
        /// <param name="nextExp"></param>
        /// <returns></returns>
        public static Expression CombineExpressions(Expression baseExp, Expression nextExp)
        {
            switch (nextExp.NodeType)
            {
                case ExpressionType.Call:
                    {
                        var mc = (MethodCallExpression)nextExp;
                        if (mc.Object == null)
                        {
                            var args = new List<Expression> { baseExp };
                            var type = baseExp.Type.GetGenericArguments().First();
                            var newParam = Expression.Parameter(type, $"p_{type.Name}");
                            foreach (var item in mc.Arguments.Skip(1))
                            {
                                var exp = item;
                                if (exp.NodeType == ExpressionType.Quote)
                                {
                                    exp = ((UnaryExpression)item).Operand;
                                }
                                var lambda = (LambdaExpression)exp;
                                exp = new ParameterReplacer().Replace(lambda, lambda.Parameters.First(), newParam);
                                args.Add(exp);
                            }
                            var call = MakeCallOnQueryable(mc.Method.Name, baseExp.Type.GetGenericArguments().ToArray(), args.ToArray());
                            return call;
                        }
                        return Expression.Call(baseExp, mc.Method, mc.Arguments);
                    }
                default: throw new EntityGraphQLCompilerException($"Could not join expressions '{baseExp.NodeType} and '{nextExp.NodeType}'");
            }
        }

        /// <summary>
        /// Navigates back through an expression to see if there was a point where we had a IEnumerable object so we can "edit" it (so a .Select() etc.)
        /// </summary>
        /// <param name="baseExpression"></param>
        /// <returns></returns>
        public static Tuple<Expression?, Expression?> FindEnumerable(Expression baseExpression)
        {
            var exp = baseExpression;
            Expression? endExpression = null;
            while (exp != null && !exp.Type.IsEnumerableOrArray())
            {
                switch (exp.NodeType)
                {
                    case ExpressionType.Call:
                        {
                            endExpression = exp;
                            var mc = (MethodCallExpression)exp;
                            exp = mc.Object ?? mc.Arguments.First();
                            if (exp.Type != baseExpression.Type && exp.Type.IsEnumerableOrArray() && exp.Type.GetEnumerableOrArrayType() != baseExpression.Type)
                                exp = null;
                            break;
                        }
                    default:
                        exp = null;
                        break;
                }
            }
            return Tuple.Create(exp, endExpression);
        }

        /// <summary>
        /// Makes a selection from a IEnumerable context
        /// </summary>
        public static Expression MakeSelectWithDynamicType(ParameterExpression currentContextParam, Expression baseExp, IDictionary<string, Expression> fieldExpressions)
        {
            if (!fieldExpressions.Any())
                return baseExp;

            var memberInit = CreateNewExpression(fieldExpressions, out Type dynamicType);
            if (memberInit == null || dynamicType == null) // nothing to select
                return baseExp;
            var selector = Expression.Lambda(memberInit, currentContextParam);
            var isQueryable = typeof(IQueryable).IsAssignableFrom(baseExp.Type);
            var call = isQueryable ? MakeCallOnQueryable("Select", new Type[] { currentContextParam.Type, dynamicType }, baseExp, selector) :
                MakeCallOnEnumerable("Select", new Type[] { currentContextParam.Type, dynamicType }, baseExp, selector);
            return call;
        }

        public static Expression? CreateNewExpression(IDictionary<string, Expression> fieldExpressions, out Type dynamicType)
        {
            var fieldExpressionsByName = new Dictionary<string, Expression>();

            foreach (var item in fieldExpressions)
            {
                // if there are duplicate fields (looking at you ApolloClient when using fragments) they override
                if (item.Value != null)
                    fieldExpressionsByName[item.Key] = item.Value;
            }

            dynamicType = typeof(object);
            if (!fieldExpressionsByName.Any())
                return null;

            dynamicType = LinqRuntimeTypeBuilder.GetDynamicType(fieldExpressionsByName.ToDictionary(f => f.Key, f => f.Value.Type));
            if (dynamicType == null)
                throw new EntityGraphQLCompilerException("Could not create dynamic type");

            var bindings = dynamicType.GetFields().Select(p => Expression.Bind(p, fieldExpressionsByName[p.Name])).OfType<MemberBinding>();
            var constructor = dynamicType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                throw new EntityGraphQLCompilerException("Could not create dynamic type");
            var newExp = Expression.New(constructor);
            var mi = Expression.MemberInit(newExp, bindings);
            return mi;
        }

        private static Expression CreateNewExpression(Dictionary<string, Expression> fieldExpressions)
        {
            var fieldExpressionsByName = new Dictionary<string, Expression>();
            foreach (var item in fieldExpressions)
            {
                // if there are duplicate fields (looking at you ApolloClient when using fragments) they override
                fieldExpressionsByName[item.Key] = item.Value;
            }
            var dynamicType = LinqRuntimeTypeBuilder.GetDynamicType(fieldExpressionsByName.ToDictionary(f => f.Key, f => f.Value.Type));

            var bindings = dynamicType.GetFields().Select(p => Expression.Bind(p, fieldExpressionsByName[p.Name])).OfType<MemberBinding>();
            var constructor = dynamicType.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                throw new EntityGraphQLCompilerException("Could not create dynamic type");
            var newExp = Expression.New(constructor);
            var mi = Expression.MemberInit(newExp, bindings);
            return mi;
        }

        /// <summary>
        /// Wrap a field expression in a method that does a null check for us and avoid calling the field multiple times.
        /// E.g. if the field is (item) => CallSomeService(item) and the result is an object (not IEnumerable) we do not want to generate
        ///     CallSomeService(item) == null ? null : new {
        ///         field1 = CallSomeService(item).field1,
        ///         field2 = CallSomeService(item).field2
        //      }
        /// As that will call the function 3 times (or 1 + number of fields selected)
        ///
        /// This wraps the field expression that does the call once
        /// </summary>
        internal static Expression WrapFieldForNullCheck(Expression nullCheckExpression, IEnumerable<ParameterExpression> paramsForFieldExpressions, Dictionary<string, Expression> fieldExpressions, IEnumerable<object> fieldSelectParamValues, ParameterExpression nullWrapParam, Expression schemaContext)
        {
            var arguments = new List<Expression> {
                nullCheckExpression,
                Expression.Constant(nullWrapParam, typeof(ParameterExpression)),
                Expression.Constant(paramsForFieldExpressions.ToList()),
                Expression.Constant(fieldExpressions),
                Expression.Constant(fieldSelectParamValues),
                schemaContext == null ? Expression.Constant(null, typeof(ParameterExpression)) : Expression.Constant(schemaContext),
                schemaContext ?? Expression.Constant(null),
            };
            var call = Expression.Call(typeof(ExpressionUtil), "WrapFieldForNullCheckExec", null, arguments.ToArray());
            return call;
        }

        /// <summary>
        /// DO NOT REMOVE. Used at runtuime.
        /// Actually implements the null check code. This is executed at execution time of the whole query not at compile time
        /// </summary>
        /// <param name="nullCheck">Object that we build the select on. Check if it is null first</param>
        /// <param name="nullWrapParam">The ParameterExpression for the null check</param>
        /// <param name="paramsForFieldExpressions">Parameters needed for the expression</param>
        /// <param name="fieldExpressions">Selection fields</param>
        /// <param name="fieldSelectParamValues">Values (arguments) for the paramsForFieldExpressions</param>
        /// <param name="schemaContextParam"></param>
        /// <param name="schemaContextValue"></param>
        /// <returns></returns>
        public static object? WrapFieldForNullCheckExec(object nullCheck, ParameterExpression nullWrapParam, List<ParameterExpression> paramsForFieldExpressions, Dictionary<string, Expression> fieldExpressions, IEnumerable<object> fieldSelectParamValues, ParameterExpression schemaContextParam, object schemaContextValue)
        {
            if (nullCheck == null)
                return null;

            var newExp = CreateNewExpression(fieldExpressions);
            var args = new List<object>();
            args.AddRange(fieldSelectParamValues);
            if (schemaContextParam != null)
            {
                args.Add(schemaContextValue);
                if (!paramsForFieldExpressions.Contains(schemaContextParam))
                    paramsForFieldExpressions.Add(schemaContextParam);
            }
            if (nullWrapParam != null)
            {
                if (!paramsForFieldExpressions.Contains(nullWrapParam))
                    paramsForFieldExpressions.Add(nullWrapParam);
                args.Add(nullCheck);
            }
            var result = Expression.Lambda(newExp, paramsForFieldExpressions).Compile().DynamicInvoke(args.ToArray());
            return result;
        }
    }
}
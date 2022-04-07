using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

        public static object ChangeType(object value, Type type)
        {
            if (value == null)
                return null;

            var objType = value.GetType();
            if (typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(objType))
            {
                value = ((Newtonsoft.Json.Linq.JToken)value).ToObject(type);
            }
            else if (type == typeof(Newtonsoft.Json.Linq.JObject))
            {
                value = ((Newtonsoft.Json.Linq.JObject)value).ToObject(type);
            }
            else if (typeof(JsonElement).IsAssignableFrom(objType))
            {
                value = ((JsonElement)value).Deserialize(type, new JsonSerializerOptions
                {
                    IncludeFields = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            if (type != typeof(string) && objType == typeof(string))
            {
                if (type == typeof(double) || type == typeof(double?))
                    return double.Parse((string)value);
                if (type == typeof(float) || type == typeof(float?))
                    return float.Parse((string)value);
                if (type == typeof(int) || type == typeof(int?))
                    return int.Parse((string)value);
                if (type == typeof(uint) || type == typeof(uint?))
                    return uint.Parse((string)value);
                if (type == typeof(DateTime) || type == typeof(DateTime?))
                    return DateTime.Parse((string)value);
                if (type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?))
                    return DateTimeOffset.Parse((string)value);
            }
            else if (type != typeof(long) && objType == typeof(long))
            {
                if (type == typeof(DateTime) || type == typeof(DateTime?))
                    return new DateTime((long)value!);
                if (type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?))
                    return new DateTimeOffset((long)value!, TimeSpan.Zero);
            }

            var argumentNonNullType = type.IsNullableType() ? Nullable.GetUnderlyingType(type) : type;
            var valueNonNullType = objType.IsNullableType() ? Nullable.GetUnderlyingType(objType) : objType;
            if (argumentNonNullType.GetTypeInfo().IsEnum)
            {
                return Enum.ToObject(argumentNonNullType, value);
            }
            if (argumentNonNullType != valueNonNullType)
            {
                var newVal = Convert.ChangeType(value, argumentNonNullType);
                return newVal;
            }
            return value;
        }

        public static Dictionary<string, ArgType> ObjectToDictionaryArgs(ISchemaProvider schema, object argTypes, Func<string, string> fieldNamer)
        {
            var args = argTypes.GetType().GetProperties().Where(p => !GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(p)).ToDictionary(k => fieldNamer(k.Name), p => ArgType.FromProperty(schema, p, p.GetValue(argTypes), fieldNamer));
            argTypes.GetType().GetFields().Where(p => !GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(p)).ToList().ForEach(p => args.Add(fieldNamer(p.Name), ArgType.FromField(schema, p, p.GetValue(argTypes), fieldNamer)));
            return args;
        }

        public static Type MergeTypes(Type type1, Type type2)
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
        public static string UpdateCollectionNodeFieldExpression(GraphQLListSelectionField collectionSelectionNode, Expression combineExpression)
        {
            string capMethod = null;
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
        public static Tuple<Expression, Expression> FindEnumerable(Expression baseExpression)
        {
            var exp = baseExpression;
            Expression endExpression = null;
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
            if (memberInit == null) // nothing to select
                return baseExp;
            var selector = Expression.Lambda(memberInit, currentContextParam);
            var isQueryable = typeof(IQueryable).IsAssignableFrom(baseExp.Type);
            var call = isQueryable ? MakeCallOnQueryable("Select", new Type[] { currentContextParam.Type, dynamicType }, baseExp, selector) :
                MakeCallOnEnumerable("Select", new Type[] { currentContextParam.Type, dynamicType }, baseExp, selector);
            return call;
        }

        public static Expression CreateNewExpression(IDictionary<string, Expression> fieldExpressions, out Type dynamicType)
        {
            var fieldExpressionsByName = new Dictionary<string, Expression>();

            foreach (var item in fieldExpressions)
            {
                // if there are duplicate fields (looking at you ApolloClient when using fragments) they override
                if (item.Value != null)
                    fieldExpressionsByName[item.Key] = item.Value;
            }

            dynamicType = null;
            if (!fieldExpressionsByName.Any())
                return null;

            dynamicType = LinqRuntimeTypeBuilder.GetDynamicType(fieldExpressionsByName.ToDictionary(f => f.Key, f => f.Value.Type));

            var bindings = dynamicType.GetFields().Select(p => Expression.Bind(p, fieldExpressionsByName[p.Name])).OfType<MemberBinding>();
            var newExp = Expression.New(dynamicType.GetConstructor(Type.EmptyTypes));
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
            var newExp = Expression.New(dynamicType.GetConstructor(Type.EmptyTypes));
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
            var call = Expression.Call(typeof(ExpressionUtil), nameof(WrapFieldForNullCheckExec), null, arguments.ToArray());
            return call;
        }

        /// <summary>
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
        public static object WrapFieldForNullCheckExec(object nullCheck, ParameterExpression nullWrapParam, List<ParameterExpression> paramsForFieldExpressions, Dictionary<string, Expression> fieldExpressions, IEnumerable<object> fieldSelectParamValues, ParameterExpression schemaContextParam, object schemaContextValue)
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
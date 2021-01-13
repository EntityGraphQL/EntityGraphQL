using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.Util
{
    internal static class ExpressionUtil
    {
        public static ExpressionResult MakeCallOnQueryable(string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            var type = typeof(IQueryable).IsAssignableFrom(parameters.First().Type) ? typeof(Queryable) : typeof(Enumerable);
            try
            {
                return (ExpressionResult)Expression.Call(type, methodName, genericTypes, parameters);
            }
            catch (InvalidOperationException ex)
            {
                throw new EntityGraphQLCompilerException($"Could not find extension method {methodName} on types {type}", ex);
            }
        }

        public static ExpressionResult MakeCallOnEnumerable(string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            var type = typeof(Enumerable);
            try
            {
                return (ExpressionResult)Expression.Call(type, methodName, genericTypes, parameters);
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
            var objType = value.GetType();
            if (typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(objType))
            {
                var newVal = ((Newtonsoft.Json.Linq.JToken)value).ToObject(type);
                return newVal;
            }

            if (type != typeof(string) && objType == typeof(string))
            {
                if (type == typeof(double) || type == typeof(Nullable<double>))
                    return double.Parse((string)value);
                if (type == typeof(float) || type == typeof(Nullable<float>))
                    return float.Parse((string)value);
                if (type == typeof(int) || type == typeof(Nullable<int>))
                    return int.Parse((string)value);
                if (type == typeof(uint) || type == typeof(Nullable<uint>))
                    return uint.Parse((string)value);
                if (type == typeof(DateTime) || type == typeof(Nullable<DateTime>))
                    return DateTime.Parse((string)value);
                if (type == typeof(DateTimeOffset) || type == typeof(Nullable<DateTimeOffset>))
                    return DateTimeOffset.Parse((string)value);
            }
            else if (type != typeof(long) && objType == typeof(long))
            {
                if (type == typeof(DateTime) || type == typeof(Nullable<DateTime>))
                    return new DateTime((long)value);
                if (type == typeof(DateTimeOffset) || type == typeof(Nullable<DateTimeOffset>))
                    return new DateTimeOffset((long)value, TimeSpan.Zero);
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

        /// <summary>
        /// Trys to take 2 expressions returned from FindIEnumerable and join them together. I.e. If we Split list.First() with FindIEnumerable, we can join it back together with newList.First()
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
        /// Naviagtes back through an expression to see if there was a point where we had a IEnumerable object so we can "edit" it (so a .Select() etc.)
        /// </summary>
        /// <param name="baseExpression"></param>
        /// <returns></returns>
        public static Tuple<Expression, Expression> FindIEnumerable(Expression baseExpression)
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
        public static Expression MakeSelectWithDynamicType(ParameterExpression currentContextParam, Expression baseExp, IDictionary<string, IGraphQLBaseNode> fieldExpressions, IServiceProvider serviceProvider, bool withoutServiceFields, ParameterExpression buildServiceWrapWithParam = null)
        {
            if (!fieldExpressions.Any())
                return baseExp;

            var memberInit = CreateNewExpression(fieldExpressions.Values, serviceProvider, withoutServiceFields, buildServiceWrapWithParam, out Type dynamicType);
            if (memberInit == null) // nothing to select
                return baseExp;
            var selector = Expression.Lambda(memberInit, currentContextParam);
            var call = MakeCallOnQueryable("Select", new Type[] { currentContextParam.Type, dynamicType }, baseExp, selector);
            return call;

            // make expression like ctx.Select(/* no service fields */) // allows ORMs/EF to optimise a select
            //                         .ToList() // forces ORMs/EF to fetch data
            //                         .Select(/* all fields */) // new projection selecting the same data plus the fields built using services

            // var fieldWithoutServices = new Dictionary<string, IGraphQLBaseNode>(StringComparer.OrdinalIgnoreCase);
            // // These are the fields we need for the last select. Some fields need to be replace from the original
            // // e.g. original might be fieldName = some.Conplex.Relation.Name
            // // Which in the last select needs to be obj.fieldName not the full expression as we are
            // // selecting from the result of the first select
            // var fieldsToReplace = new HashSet<string>();
            // var resultFields = fieldExpressions.ToDictionary(i => i.Key, i => i.Value);
            // var fieldsWithoutServicesCount = 0;
            // foreach (var fieldExp in fieldExpressions.Values)
            // {
            //     if (!fieldExp.HasWrappedService)
            //     {
            //         // we need to replace the expression as it might be complex selection that follows relations
            //         // and the last select only has the first Select result
            //         // replace it below when we have type infomation
            //         fieldsToReplace.Add(fieldExp.Name);

            //         fieldWithoutServices[fieldExp.Name] = fieldExp;
            //         fieldsWithoutServicesCount += 1;
            //     }
            //     else
            //     {
            //         // figure out if we need to include a field on currentContextParam required by this service
            //         foreach (var item in fieldExp.GetFieldsWithoutServices(currentContextParam))
            //         {
            //             // If they use the context parameter we can't just select the whole object as we don't know
            //             // what other objects in the graph they might be using in the service - which may not be loaded by EF
            //             // GetSubExpressionForParameter() above will throw an exception

            //             if (!fieldWithoutServices.ContainsKey(item.Name))
            //                 fieldWithoutServices[item.Name] = item;
            //         }
            //     }
            // }

            // Expression result = baseExp;
            // Type dynamicTypeNoServices = currentContextParam.Type;
            // if (fieldWithoutServices.Any())
            // {
            //     // create a select with all the non-service fields built of the original currentContextParam
            //     var memberInit = CreateNewExpression(fieldWithoutServices.Values, serviceProvider, out dynamicTypeNoServices);
            //     var selector = Expression.Lambda(memberInit, currentContextParam);
            //     result = MakeCallOnQueryable("Select", new Type[] { currentContextParam.Type, dynamicTypeNoServices }, result, selector);
            // }

            // if (fieldsWithoutServicesCount != resultFields.Count())
            // {
            //     // we have selected all fields without services (means it'll work with Entity Framework)
            //     // Now call ToList() to force Evaluation with ORMs
            //     result = MakeCallOnEnumerable("ToList", new Type[] { dynamicTypeNoServices }, result);

            //     // call Select() with all originally requested fields - includes those that use services
            //     var withServicesParameter = Expression.Parameter(dynamicTypeNoServices);
            //     // Build new selection now we have type and parameter
            //     foreach (var fieldName in fieldsToReplace)
            //     {
            //         resultFields[fieldName] = new GraphQLQueryNode(null, null, fieldName, (ExpressionResult)Expression.Field(withServicesParameter, fieldName), null, null, null);
            //     }
            //     var allMembersInit = CreateNewExpression(resultFields.Values, serviceProvider, out Type dynamicTypeWithServices);
            //     allMembersInit = replacer.Replace(allMembersInit, currentContextParam, withServicesParameter);
            //     var selectorAllFields = Expression.Lambda(allMembersInit, withServicesParameter);
            //     result = MakeCallOnEnumerable("Select", new Type[] { dynamicTypeNoServices, dynamicTypeWithServices }, result, selectorAllFields);
            // }

            // return result;
        }

        public static Expression CreateNewExpression(IDictionary<string, IGraphQLBaseNode> fieldExpressions, IServiceProvider serviceProvider, bool withoutServiceFields, ParameterExpression buildServiceWrapWithParam)
        {
            var memberInit = CreateNewExpression(fieldExpressions.Values, serviceProvider, withoutServiceFields, buildServiceWrapWithParam, out _);
            return memberInit;
        }

        private static Expression CreateNewExpression(IEnumerable<IGraphQLBaseNode> fieldExpressions, IServiceProvider serviceProvider, bool withoutServiceFields, ParameterExpression buildServiceWrapWithParam, out Type dynamicType)
        {
            var fieldExpressionsByName = new Dictionary<string, ExpressionResult>();
            foreach (var item in fieldExpressions)
            {
                // if there are duplicate fields (looking at you ApolloClient when using fragments) they override
                var expressionResult = item.GetNodeExpression(null, serviceProvider, withoutServiceFields, buildServiceWrapWithParam);
                if (expressionResult != null)
                    fieldExpressionsByName[item.Name] = expressionResult;
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

        private static Expression CreateNewExpression(Dictionary<string, ExpressionResult> fieldExpressions)
        {
            var fieldExpressionsByName = new Dictionary<string, ExpressionResult>();
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
        internal static ExpressionResult WrapFieldForNullCheck(ExpressionResult nullCheckExpression, ParameterExpression selectionContextExpression, IEnumerable<ParameterExpression> paramsForFieldExpressions, Dictionary<string, ExpressionResult> fieldExpressions, IEnumerable<object> fieldSelectParamValues, ParameterExpression nullWrapParam)
        {
            var arguments = new List<Expression> {
                nullCheckExpression,
                selectionContextExpression,
                Expression.Constant(nullWrapParam, typeof(ParameterExpression)),
                Expression.Constant(paramsForFieldExpressions.ToList()),
                Expression.Constant(fieldExpressions),
                Expression.Constant(fieldSelectParamValues),
            };
            var call = Expression.Call(typeof(ExpressionUtil), "WrapFieldForNullCheckExec", null, arguments.ToArray());
            return (ExpressionResult)call;
        }

        /// <summary>
        /// Actually implements the null check code. This is executed at execution time of the whole query not at compile time
        /// </summary>
        /// <returns></returns>
        public static object WrapFieldForNullCheckExec(object nullCheck, object selectionContext, ParameterExpression nullWrapParam, List<ParameterExpression> paramsForFieldExpressions, Dictionary<string, ExpressionResult> fieldExpressions, IEnumerable<object> fieldSelectParamValues)
        {
            if (nullCheck == null)
                return null;

            var newExp = CreateNewExpression(fieldExpressions);
            var args = new List<object> { selectionContext };
            args.AddRange(fieldSelectParamValues);
            if (nullWrapParam != null)
            {
                paramsForFieldExpressions.Add(nullWrapParam);
                args.Add(nullCheck);
            }
            var result = Expression.Lambda(newExp, paramsForFieldExpressions).Compile().DynamicInvoke(args.ToArray());
            return result;
        }
    }
}
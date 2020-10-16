﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.Util
{
    public static class ExpressionUtil
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

        public static Expression SelectDynamicToList(ParameterExpression currentContextParam, Expression baseExp, IEnumerable<IGraphQLBaseNode> fieldExpressions, IServiceProvider serviceProvider)
        {
            var memberInit = CreateNewExpression(fieldExpressions, serviceProvider, out Type dynamicType);
            var selector = Expression.Lambda(memberInit, currentContextParam);
            var call = MakeCallOnQueryable("Select", new Type[2] { currentContextParam.Type, dynamicType }, baseExp, selector);
            return call;
        }

        public static Expression CreateNewExpression(IEnumerable<IGraphQLBaseNode> fieldExpressions, IServiceProvider serviceProvider)
        {
            var memberInit = CreateNewExpression(fieldExpressions, serviceProvider, out _);
            return memberInit;
        }
        private static Expression CreateNewExpression(IEnumerable<IGraphQLBaseNode> fieldExpressions, IServiceProvider serviceProvider, out Type dynamicType)
        {
            var fieldExpressionsByName = new Dictionary<string, ExpressionResult>();
            foreach (var item in fieldExpressions)
            {
                // if there are duplicate fields (looking at you ApolloClient when using fragments) they override
                fieldExpressionsByName[item.Name] = item.GetNodeExpression(null, serviceProvider);
            }
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
        /// E.g. if the field is (db) => CallSomeService(db) and the result is an object (not IEnumerable) we do not want to generate
        ///     CallSomeService(db) == null ? null : new {
        ///         field1 = CallSomeService(db).field1,
        ///         field2 = CallSomeService(db).field2
        //      }
        /// As that will call the function 3 times (or 1 + number of fields selected)
        ///
        /// This wraps the field expression that does the call once
        /// </summary>
        /// <param name="selectFromExp"></param>
        /// <param name="replacementParameter"></param>
        /// <param name="fieldExpressions"></param>
        /// <returns></returns>
        internal static ExpressionResult WrapFieldForNullCheck(ExpressionResult selectFromExp, IEnumerable<ParameterExpression> paramsForFieldExpressions, Dictionary<string, ExpressionResult> fieldExpressions, IEnumerable<object> fieldSelectParamValues)
        {
            var arguments = new List<Expression> {
                selectFromExp,
                Expression.Constant(paramsForFieldExpressions),
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
        public static object WrapFieldForNullCheckExec(object selectFromValue, IEnumerable<ParameterExpression> paramsForFieldExpressions, Dictionary<string, ExpressionResult> fieldExpressions, IEnumerable<object> fieldSelectParamValues)
        {
            if (selectFromValue == null)
                return null;

            var newExp = CreateNewExpression(fieldExpressions);
            var args = new List<object> { selectFromValue };
            args.AddRange(fieldSelectParamValues);
            var result = Expression.Lambda(newExp, paramsForFieldExpressions.ToArray()).Compile().DynamicInvoke(args.ToArray());
            return result;
        }
    }
}

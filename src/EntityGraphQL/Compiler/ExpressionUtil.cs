using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Extensions;
using EntityGraphQL.Parsing;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class ExpressionUtil
    {
        public static ExpressionResult MakeExpressionCall(Type[] types, string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            foreach (var t in types)
            {
                // Please tell me a better way to do this!
                try
                {
                    //  Console.WriteLine($"Call({t}, {methodName}, {genericTypes}, {parameters.First()})");
                    return (ExpressionResult)Expression.Call(t, methodName, genericTypes, parameters);
                }
                catch (InvalidOperationException)
                {
                    continue; // to next type
                }
            }
            var typesStr = string.Join<Type>(", ", types);
            throw new EqlCompilerException($"Could not find extension method {methodName} on types {typesStr}");
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
            if (type != typeof(string) && objType == typeof(string)) {
                if (type == typeof(double) || type == typeof(Nullable<double>))
                    return double.Parse((string)value);
                if (type == typeof(float) || type == typeof(Nullable<float>))
                    return float.Parse((string)value);
                if (type == typeof(int) || type == typeof(Nullable<int>))
                    return int.Parse((string)value);
                if (type == typeof(uint) || type == typeof(Nullable<uint>))
                    return uint.Parse((string)value);
            }
            var nonNullType = type.IsNullableType() ? Nullable.GetUnderlyingType(type) : type;
            var nonNullObjType = objType.IsNullableType() ? Nullable.GetUnderlyingType(objType) : objType;
            if (nonNullType != nonNullObjType)
            {
                var newVal = Convert.ChangeType(value, type);
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
                case ExpressionType.Call: {
                    var mc = (MethodCallExpression)nextExp;
                    if (mc.Object == null)
                    {
                        var args = new List<Expression> { baseExp };
                        args.AddRange(mc.Arguments.Skip(1));
                        return Expression.Call(mc.Method.DeclaringType, mc.Method.Name, new Type[] {baseExp.Type.GetGenericArguments()[0]}, args.ToArray());
                    }
                    return Expression.Call(baseExp, mc.Method, mc.Arguments);
                }
                default: throw new EqlCompilerException($"Could not join expressions '{baseExp.NodeType} and '{nextExp.NodeType}'");
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
            while (exp != null && !exp.Type.IsEnumerable())
            {
                switch (exp.NodeType)
                {
                    case ExpressionType.Call: {
                        endExpression = exp;
                        var mc = (MethodCallExpression)exp;
                        exp = mc.Object != null ? mc.Object : mc.Arguments.First();
                        break;
                    }
                    default: exp = null;
                        break;
                }
            }
            return Tuple.Create(exp, endExpression);
        }

        public static Expression SelectDynamic(ParameterExpression currentContextParam, Expression baseExp, IEnumerable<IGraphQLNode> fieldExpressions, ISchemaProvider schemaProvider)
        {
            Type dynamicType;
            var memberInit = CreateNewExpression(currentContextParam, fieldExpressions, schemaProvider, out dynamicType);
            var selector = Expression.Lambda(memberInit, currentContextParam);
            return ExpressionUtil.MakeExpressionCall(new [] {typeof(Queryable), typeof(Enumerable)}, "Select", new Type[2] { currentContextParam.Type, dynamicType }, baseExp, selector);
        }

        public static Expression CreateNewExpression(Expression currentContext, IEnumerable<IGraphQLNode> fieldExpressions, ISchemaProvider schemaProvider)
        {
            Type dynamicType;
            var memberInit = CreateNewExpression(currentContext, fieldExpressions, schemaProvider, out dynamicType);
            return memberInit;
        }
        public static Expression CreateNewExpression(Expression currentContext, IEnumerable<IGraphQLNode> fieldExpressions, ISchemaProvider schemaProvider, out Type dynamicType)
        {
            var fieldExpressionsByName = fieldExpressions.ToDictionary(f => f.Name, f => f.NodeExpression);
            dynamicType = LinqRuntimeTypeBuilder.GetDynamicType(fieldExpressions.ToDictionary(f => f.Name, f => f.NodeExpression.Type));

            var bindings = dynamicType.GetFields().Select(p => Expression.Bind(p, fieldExpressionsByName[p.Name])).OfType<MemberBinding>();
            var newExp = Expression.New(dynamicType.GetConstructor(Type.EmptyTypes));
            var mi = Expression.MemberInit(newExp, bindings);
            return mi;
        }
    }
}
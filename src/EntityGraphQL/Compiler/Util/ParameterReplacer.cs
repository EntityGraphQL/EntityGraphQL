using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.Util
{
    /// <summary>
    /// As people build schema fields they may be against different parameters, this visitor lets us change it to the one used in compiling the EQL.
    /// I.e. in (p_0) => p_0.blah `p_0` needs to be the same object otherwise it expects 2 values passed in.
    /// </summary>
    public class ParameterReplacer : ExpressionVisitor
    {
        private Expression? newParam;
        private Type? toReplaceType;
        private ParameterExpression? toReplace;
        private bool finished;
        private bool replaceWholeExpression;
        private string? newFieldNameForType;
        private Dictionary<object, Expression> cache = new();
        private bool hasNewFieldNameForType;

        /// <summary>
        /// Rebuilds the expression by replacing toReplace with newParam. Optionally looks for newFieldName as it is rebuilding.
        /// Used when rebuilding expressions to happen from the sans-services results
        /// </summary>
        /// <param name="node"></param>
        /// <param name="toReplace"></param>
        /// <param name="newParam"></param>
        /// <param name="newFieldName"></param>
        /// <returns></returns>
        public Expression Replace(Expression node, ParameterExpression toReplace, Expression newParam, bool replaceWholeExpression = false)
        {
            this.newParam = newParam;
            this.toReplace = toReplace;
            this.toReplaceType = null;
            this.newFieldNameForType = null;
            finished = false;
            this.replaceWholeExpression = replaceWholeExpression;
            cache = new Dictionary<object, Expression>();
            return Visit(node);
        }

        /// <summary>
        /// Replace an expression of type with newParam.
        /// Try to avoid using this if possible as there might be multiple of the type in the expression that you do not want to replace.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="toReplaceType"></param>
        /// <param name="newParam"></param>
        /// <param name="newContextFieldName"></param>
        /// <returns></returns>
        public Expression ReplaceByType(Expression node, Type toReplaceType, Expression newParam, string? newContextFieldName = null)
        {
            this.newParam = newParam;
            this.toReplaceType = toReplaceType;
            this.toReplace = null;
            finished = false;
            this.newFieldNameForType = newContextFieldName;
            hasNewFieldNameForType = newFieldNameForType != null;
            replaceWholeExpression = false;
            cache = new Dictionary<object, Expression>();
            return Visit(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (toReplace != null && toReplace == node)
            {
                return newParam!;
            }
            if (toReplaceType != null && node.NodeType == ExpressionType.Parameter && toReplaceType == node.Type)
                return newParam!;
            return node;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            var p = node.Parameters.Select(base.Visit).Cast<ParameterExpression>();
            var body = base.Visit(node.Body);
            return Expression.Lambda(body, p);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            // RequiredField<> causes a convert
            if (node.Operand != null)
            {
                var newNode = base.Visit(node.Operand);
                if (node.NodeType == ExpressionType.Convert && node.Type == newNode.Type)
                    return newNode;
                return Expression.MakeUnary(node.NodeType, newNode, node.Type);
            }
            return base.VisitUnary(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // returned expression may have been modified and we need to rebuild
            if (node.Expression != null)
            {
                if (replaceWholeExpression)
                {
                    // RequiredField<> causes a ctx.Field.Value
                    if (node.Expression == toReplace || (node.Expression.NodeType == ExpressionType.MemberAccess && ((MemberExpression)node.Expression).Expression == toReplace))
                    {
                        return newParam!;
                    }
                }

                Expression nodeExp;
                var fieldName = node.Member.Name;
                if (node.Expression.Type == toReplaceType)
                {
                    nodeExp = newParam!;
                    if (hasNewFieldNameForType)
                        fieldName = newFieldNameForType!;
                }
                else
                    nodeExp = base.Visit(node.Expression);

                if (finished)
                    return nodeExp;

                var field = nodeExp.Type.GetField(fieldName);
                if (field != null)
                    nodeExp = Expression.Field(nodeExp, field);
                else
                {
                    var prop = nodeExp.Type.GetProperty(fieldName);
                    if (prop != null)
                        nodeExp = Expression.Property(nodeExp, prop);
                    else
                        nodeExp = Expression.PropertyOrField(nodeExp, node.Member.Name);
                }
                return nodeExp;
            }
            return base.VisitMember(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // we do not want to replace constant ParameterExpressions in a nullwrap            
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            bool baseCallIsEnumerable = node.Object == null && node.Arguments[0].Type.IsEnumerableOrArray();
            if (node.Object == null && node.Arguments.Count > 1 && node.Method.IsGenericMethod)
            {
                // Replace expression that are inside method calls that might need parameters updated (.Where() etc.)
                var key = node.Arguments[0];
                // if we have already replaced this expression then use the cached version. 
                // e.g. Extension method where the same expression is passed to each method
                cache.TryGetValue(key, out Expression? callBase);
                if (callBase == null)
                {
                    callBase = base.Visit(key);
                    cache[key] = callBase;
                }

                var callBaseType = callBase.Type.IsEnumerableOrArray() ? callBase.Type.GetEnumerableOrArrayType()! : callBase.Type;
                var oldCallBaseType = baseCallIsEnumerable ? node.Arguments[0].Type.GetEnumerableOrArrayType()! : node.Arguments[0].Type;
                if (callBaseType != oldCallBaseType)
                {
                    var replaceAgain = new ParameterReplacer();
                    var newTypeArgs = new List<Type>(node.Arguments.Count) { callBaseType };
                    var newArgs = new List<Expression>(node.Arguments.Count) { callBase };
                    var oldTypeArgs = node.Method.GetGenericArguments();
                    foreach (var oldArg in node.Arguments.Skip(1))
                    {
                        var newArg = replaceAgain.ReplaceByType(oldArg, oldCallBaseType, Expression.Parameter(callBaseType));
                        newArgs.Add(newArg);
                        if (newArg.NodeType == ExpressionType.Lambda && oldTypeArgs.Contains(((LambdaExpression)newArg).ReturnType))
                            newTypeArgs.Add(((LambdaExpression)newArg).ReturnType);
                        else if (oldTypeArgs.Contains(oldArg.Type))
                            newTypeArgs.Add(newArg.Type);
                    }
                    if (oldTypeArgs.Length != newTypeArgs.Count)
                        throw new EntityGraphQLCompilerException($"Post service object selection contains a method call with mismatched generic type arguments.");
                    var newCall = Expression.Call(node.Method.DeclaringType!, node.Method.Name, newTypeArgs.ToArray(), newArgs.ToArray());
                    return newCall;
                }
            }
            else if (baseCallIsEnumerable && !node.Type.IsEnumerableOrArray() && node.Arguments.Count == 1 && hasNewFieldNameForType)
            {
                // field is going from collection to a single - if execution is split over non service fields and then with
                // the next context doesn't have the collection to single. It only has the single
                var newField = base.Visit(node.Arguments[0]);
                if (newField != null)
                    return newField;
            }

            var callOn = node.Object;
            if (callOn != null)
            {
                if (callOn.Type == toReplaceType)
                    callOn = newParam!;
                else
                    callOn = base.Visit(callOn);
            }

            return Expression.Call(callOn, node.Method, node.Arguments.Select(base.Visit).ToArray()!);
        }
    }
}
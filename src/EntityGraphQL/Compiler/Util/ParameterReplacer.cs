using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;
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
        private Expression? toReplace;
        private bool finished;
        private bool replaceWholeExpression;
        private string? newFieldNameForType;
        private Dictionary<object, Expression> cache = [];
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
        public Expression Replace(Expression node, Expression toReplace, Expression newParam, bool replaceWholeExpression = false)
        {
            if (node == toReplace)
                return newParam;

            this.newParam = newParam;
            this.toReplace = toReplace;
            this.toReplaceType = null;
            this.newFieldNameForType = null;
            finished = false;
            this.replaceWholeExpression = replaceWholeExpression;
            cache = [];
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
                return newParam!;

            if (toReplaceType != null && node.NodeType == ExpressionType.Parameter && toReplaceType == node.Type)
                return newParam!;
            return node;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            if (toReplace != null && toReplace == node)
                return newParam!;

            var p = node.Parameters.Select(base.Visit).Cast<ParameterExpression>();
            var body = base.Visit(node.Body);
            return Expression.Lambda(body, p);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (toReplace != null && toReplace == node)
                return newParam!;

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
            if (toReplace != null && toReplace == node)
                return newParam!;

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
                    {
                        try
                        {
                            // extracted fields get flatten as they are selected in the first pass. The new expression can be built
                            nodeExp = Expression.PropertyOrField(nodeExp, node.Member.Name);
                        }
                        catch (ArgumentException)
                        {
                            if (nodeExp == null)
                            {
                                throw new EntityGraphQLCompilerException($"Could not find field {node.Member.Name} on type {node.Type.Name}");
                            }
                        }
                    }
                }
                return nodeExp;
            }
            return base.VisitMember(node);
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            if (toReplace != null && toReplace == node)
                return newParam!;

            return base.VisitMemberInit(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (toReplace != null && toReplace == node)
                return newParam!;

            var left = base.Visit(node.Left);
            var right = base.Visit(node.Right);
            // as we do not replace constants we need to make sure the types match as
            // we now have dynamic types and the null might be of the original type
            var leftNonNullableType = left.Type.GetNonNullableType();
            var rightNonNullableType = right.Type.GetNonNullableType();
            if (left.NodeType == ExpressionType.Constant && leftNonNullableType != rightNonNullableType)
            {
                left = Expression.Constant(null, right.Type);
            }
            if (right.NodeType == ExpressionType.Constant && rightNonNullableType != leftNonNullableType)
            {
                right = Expression.Constant(null, left.Type);
            }
            var bin = Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
            return bin;
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if (toReplace != null && toReplace == node)
                return newParam!;

            var test = base.Visit(node.Test);
            var ifTrue = base.Visit(node.IfTrue);
            var ifFalse = base.Visit(node.IfFalse);
            var type = node.Type;
            if (node.Type.IsEnumerableOrArray() && (node.Type != ifTrue.Type || node.Type != ifFalse.Type))
            {
                // we may have changed a IEnumerable<T> to an IEnumerable<TDynamic> where TDynamic is a dynamic type
                // built for the graph selection

                // We create a NewArrayInit as part of the bulk service loading
                if (ifTrue.NodeType == ExpressionType.NewArrayInit)
                {
                    var ifFalseType = ifFalse.Type.GetEnumerableOrArrayType()!;
                    ifTrue = Expression.NewArrayInit(ifFalseType);
                    type = typeof(IEnumerable<>).MakeGenericType(ifFalseType);
                }
                if (ifFalse.NodeType == ExpressionType.NewArrayInit)
                {
                    var ifTrueType = ifTrue.Type.GetEnumerableOrArrayType()!;
                    ifFalse = Expression.NewArrayInit(ifTrueType);
                    type = typeof(IEnumerable<>).MakeGenericType(ifTrueType);
                }
            }
            var cond = Expression.Condition(test, ifTrue, ifFalse, type);
            return cond;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (toReplace != null && toReplace == node)
                return newParam!;

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
                        if (oldArg is LambdaExpression oldLambda)
                        {
                            var newArg = (LambdaExpression)replaceAgain.Replace(oldArg, ((LambdaExpression)oldArg).Parameters[0], Expression.Parameter(callBaseType));
                            newArgs.Add(newArg);
                            var argTypeNonList = newArg.ReturnType.IsEnumerableOrArray() ? newArg.ReturnType.GetEnumerableOrArrayType()! : null;
                            var oldArgTypeNonList = oldLambda.ReturnType.IsEnumerableOrArray() ? oldLambda.ReturnType.GetEnumerableOrArrayType()! : null;
                            if (oldTypeArgs.Contains(oldLambda.ReturnType))
                                newTypeArgs.Add(newArg.ReturnType);
                            else if (argTypeNonList != null && oldTypeArgs.Contains(oldArgTypeNonList))
                                newTypeArgs.Add(argTypeNonList);
                        }
                        else
                        {
                            newArgs.Add(oldArg);
                            if (oldTypeArgs.Contains(oldArg.Type))
                                newTypeArgs.Add(oldArg.Type);
                        }
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

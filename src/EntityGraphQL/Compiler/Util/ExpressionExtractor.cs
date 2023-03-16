using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.Util
{
    /// <summary>
    /// Extracts expression with the root context as the provided ParameterExpression.
    /// Useful for getting required fields out of a ResolveWithService() call.
    /// For example if the full expression is the below and the root context is ctx
    ///     myService.CallThis(ctx.Field1, otherService.Call(ctx.Child.Field2))
    /// We extract the following expressions:
    ///    ctx.Field1
    ///    ctx.Child.Field2
    /// </summary>
    public class ExpressionExtractor : ExpressionVisitor
    {
        private readonly Regex pattern = new("[\\.\\(\\)\\!]");

        private Expression? rootContext;
        // We extract all expression - which may repeat - and we then replace them by matching the expression object
        private Dictionary<string, List<Expression>>? extractedExpressions;
        /// <summary>
        /// Current expression we might extract. 
        /// </summary>
        private readonly Stack<Expression> currentExpression = new();
        private bool matchByType;

        public IDictionary<string, List<Expression>>? Extract(Expression node, Expression rootContext, bool matchByType = false)
        {
            this.rootContext = rootContext;
            extractedExpressions = new Dictionary<string, List<Expression>>();
            currentExpression.Clear();
            this.matchByType = matchByType;
            Visit(node);
            return extractedExpressions.Count > 0 ? extractedExpressions : null;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (rootContext == null)
                throw new EntityGraphQLCompilerException("Root context not set for ExpressionExtractor");

            if ((rootContext == node || (matchByType && rootContext.Type == node.Type)) && currentExpression.Count > 0)
            {
                var expressionItem = currentExpression.Peek();
                // use the expression as the extracted field name as it will be unique
                var name = pattern.Replace(expressionItem.ToString(), "_");
                if (!extractedExpressions!.ContainsKey(name))
                    extractedExpressions![name] = new List<Expression> { expressionItem };
                else
                    extractedExpressions![name].Add(expressionItem);
            }
            return base.VisitParameter(node);
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            // if is is a nullable type we want to extract the nullable field not the nullableField.HasValue/Value
            // node.Expression can be null if it is a static member - e.g. DateTime.MaxValue
            // if it is empty this is the end of an expression too
            if ((currentExpression.Count == 0 && node.Expression?.Type.IsNullableType() == false) || node.Type.IsNullableType() == true)
            {
                currentExpression.Push(node);
                var result = base.VisitMember(node);
                currentExpression.Pop();
                return result;
            }
            return base.VisitMember(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            ProcessPotentialLeaf(node.Left);
            ProcessPotentialLeaf(node.Right);

            return node;
        }

        private void ProcessPotentialLeaf(Expression node)
        {
            var shouldAddNotAdd = node.NodeType == ExpressionType.MemberAccess && ((MemberExpression)node).Expression?.Type.IsNullableType() == true;
            if (shouldAddNotAdd)
                Visit(node);
            else
            {
                currentExpression.Push(node);
                Visit(node);
                currentExpression.Pop();
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object != null)
            {
                if (currentExpression.Count == 0)
                {
                    // only need to extract if this is not part of a larger expression
                    currentExpression.Push(node);
                    Visit(node.Object);
                    currentExpression.Pop();
                }
                else
                    Visit(node.Object);
            }
            var startAt = 0;
            // only need to extract if this is not part of a larger expression
            // and only visit args[0] with the whole node if it is an extension method
            bool isExtension = node.Method.IsDefined(typeof(ExtensionAttribute), true);
            if (node.Object is null && isExtension)
            {
                startAt = 1;
                if (currentExpression.Count == 0)
                {
                    currentExpression.Push(node);
                    Visit(node.Arguments[0]);
                    currentExpression.Pop();
                }
                else
                    Visit(node.Arguments[0]);
            }
            for (int i = startAt; i < node.Arguments.Count; i++)
            {
                // each arg might be extractable but we should end up back in a acll or member access again
                ProcessPotentialLeaf(node.Arguments[i]);
            }
            return node;
        }
    }
}
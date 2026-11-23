using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// Extracts expression with the root context as the provided ParameterExpression.
/// Useful for getting required fields out of a Resolve<TService>() call.
/// For example if the full expression is the below and the root context is ctx
///     myService.CallThis(ctx.Field1, otherService.Call(ctx.Child.Field2))
/// We extract the following expressions:
///    ctx.Field1
///    ctx.Child.Field2
/// </summary>
public partial class ExpressionExtractor : ExpressionVisitor
{
    private readonly Regex replacePattern = ReplacePattern();

    private Expression? rootContext;

    // We extract all expression - which may repeat - and we then replace them by matching the expression object
    private Dictionary<string, List<Expression>>? extractedExpressions;

    /// <summary>
    /// Current expression we might extract.
    /// </summary>
    private readonly Stack<Expression> currentExpression = new();
    private bool matchByType;
    private List<ParameterExpression>? serviceParameters;

    public IDictionary<string, List<Expression>>? Extract(Expression node, Expression rootContext, bool matchByType = false) => Extract(node, rootContext, matchByType, null);

    /// <param name="serviceParameters">The parameters of the expression that are services. Nothing that uses one is extracted</param>
    public IDictionary<string, List<Expression>>? Extract(Expression node, Expression rootContext, bool matchByType, IEnumerable<ParameterExpression>? serviceParameters)
    {
        this.serviceParameters = serviceParameters == null ? null : [.. serviceParameters];
        this.rootContext = rootContext;
        extractedExpressions = [];
        currentExpression.Clear();
        this.matchByType = matchByType;
        Visit(node);
        return extractedExpressions.Count > 0 ? extractedExpressions : null;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (rootContext == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, "Root context not set for ExpressionExtractor");

        if ((rootContext == node || (matchByType && rootContext.Type == node.Type)) && currentExpression.Count > 0)
        {
            var expressionItem = currentExpression.Peek();
            // use the expression as the extracted field name as it will be unique
            var name = "egql__" + replacePattern.Replace(expressionItem.ToString(), "_");
            if (extractedExpressions!.TryGetValue(name, out var existing))
            {
                existing.Add(expressionItem);
            }
            else
            {
                extractedExpressions[name] = [expressionItem];
            }
        }
        return base.VisitParameter(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // if is is a nullable type we want to extract the nullable field not the nullableField.HasValue/Value
        // node.Expression can be null if it is a static member - e.g. DateTime.MaxValue
        // if it is empty this is the end of an expression too
        if (((currentExpression.Count == 0 && node.Expression?.Type.IsNullableType() == false) || node.Type.IsNullableType()) && !UsesService(node))
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
        // A construction is not a leaf - it is a wrapper around the reads that build it. Visiting it with itself on
        // the stack credits every one of those reads to the construction, so `srv.Get(new Key(ctx.A, ctx.B))` extracts
        // that one node twice instead of ctx.A and ctx.B. Visit it without pushing so each read credits itself.
        // The same goes for anything that uses a service - see UsesService.
        var shouldAddNotAdd =
            (node.NodeType == ExpressionType.MemberAccess && ((MemberExpression)node).Expression?.Type.IsNullableType() == true)
            || node.NodeType == ExpressionType.New
            || node.NodeType == ExpressionType.MemberInit
            || UsesService(node);
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
            VisitCallTarget(node, node.Object);
        var startAt = 0;
        // only visit args[0] with the whole node if it is an extension method
        bool isExtension = node.Method.IsDefined(typeof(ExtensionAttribute), true);
        if (node.Object is null && isExtension)
        {
            startAt = 1;
            VisitCallTarget(node, node.Arguments[0]);
        }
        for (int i = startAt; i < node.Arguments.Count; i++)
        {
            // each arg might be extractable but we should end up back in a call or member access again
            ProcessPotentialLeaf(node.Arguments[i]);
        }
        return node;
    }

    /// <summary>
    /// Visit what a call is made on - the instance, or the first argument of an extension method.
    /// </summary>
    private void VisitCallTarget(MethodCallExpression call, Expression target)
    {
        if (currentExpression.Count > 0)
            // part of a larger expression, which is the one to extract
            Visit(target);
        else if (UsesService(call))
            // the call can not be extracted whole (see UsesService) so extract what it is made on
            ProcessPotentialLeaf(target);
        else
        {
            currentExpression.Push(call);
            Visit(target);
            currentExpression.Pop();
        }
    }

    /// <summary>
    /// True when <paramref name="expression"/> uses a service anywhere, including inside a nested lambda. An
    /// extracted expression runs in the first pass, where no service is available, so such an expression is
    /// never extracted whole - <c>user.Extra(srv.Current())</c> and <c>user.Tags.Count(t => srv.Ok(t))</c>
    /// extract <c>user</c> and <c>user.Tags</c>. Everything on <see cref="currentExpression"/> is therefore
    /// free of services, and so is everything below it.
    /// </summary>
    private bool UsesService(Expression expression) => serviceParameters?.Count > 0 && AggregateReductionSplit.UsesAny(expression, serviceParameters);

    [GeneratedRegex("[\\.\\(\\)\\!]")]
    private static partial Regex ReplacePattern();
}

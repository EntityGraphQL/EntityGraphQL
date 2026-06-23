using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public class GraphQLScalarField : BaseGraphQLField
{
    public GraphQLScalarField(
        ISchemaProvider schema,
        IField field,
        string name,
        Expression nextFieldContext,
        ParameterExpression? rootParameter,
        IGraphQLNode parentNode,
        IReadOnlyDictionary<string, object?>? arguments
    )
        : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments) { }

    protected override Expression? GetFieldExpression(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        // A scalar field whose resolver reduces a collection using a service (e.g. db.Movies.Sum(m => svc.Score(m.Id)))
        // can't run in one pass against EF (the service isn't translatable). Split it: pass 1 materializes a
        // DB-translatable projection of the element values the service needs; pass 2 runs the reduction in memory.
        if (HasServices && Field?.ResolveExpression != null && Field.FieldParam != null && AggregateReductionSplit.TryCreate(Field.ResolveExpression, Field.Services, out var split))
        {
            if (withoutServiceFields)
            {
                var deps = split!.BuildDepsProjection();
                if (schemaContext != null)
                    deps = replacer.Replace(deps, Field.FieldParam, schemaContext);
                var depsElementType = deps.Type.GetEnumerableOrArrayType()!;
                return Expression.Call(typeof(System.Linq.Enumerable), nameof(System.Linq.Enumerable.ToList), [depsElementType], deps);
            }
            if (contextChanged && replacementNextFieldContext != null)
            {
                compileContext.AddServices(Field.Services);
                return ProcessScalarExpression(split!.BuildReduce(replacementNextFieldContext), replacer);
            }
        }

        if (HasServices && withoutServiceFields)
            return null;

        var nextFieldContext = HandleBulkServiceResolver(compileContext, withoutServiceFields, NextFieldContext)!;

        // We need to swap the context first as GetExpression() below may change the expression if it uses the arguments
        // and the expressions will no longer match in ReplaceContext
        // example: field is (x => srv.Something(x.Name, args.input))
        // x.Name needs to be replaced before GetExpression() fixes up the execution args type
        // this is for service fields that have parameters that reference the context and the query args
        // See test InheritanceTestUsingResolveWithServiceUsingArgs
        if (contextChanged && replacementNextFieldContext != null)
        {
            nextFieldContext = ReplaceContext(replacementNextFieldContext, replacer, nextFieldContext!, possibleNextContextTypes);
        }

        HandleBeforeRootFieldExpressionBuild(compileContext, GetOperationName(this), Name, contextChanged, IsRootField, ref nextFieldContext);

        (var result, _) = Field!.GetExpression(
            nextFieldContext,
            replacementNextFieldContext,
            this,
            schemaContext,
            compileContext,
            Arguments,
            docParam,
            docVariables,
            Directives,
            contextChanged,
            withoutServiceFields,
            replacer
        );

        if (result == null)
            return null;

        var newExpression = result;

        newExpression = ProcessScalarExpression(newExpression, replacer);

        if (HasServices)
            compileContext.AddServices(Field.Services);
        return newExpression;
    }
}

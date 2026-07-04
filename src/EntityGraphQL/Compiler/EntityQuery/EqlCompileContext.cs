using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery;

public class EqlCompileContext : CompileContext
{
    public EqlCompileContext(CompileContext compileContext)
        : base(
            compileContext.ExecutionOptions,
            compileContext.BulkData,
            compileContext.RequestContext,
            compileContext.DocumentVariablesParameter,
            compileContext.DocumentVariables,
            compileContext.CancellationToken
        ) { }

    public List<IField> ServiceFieldDependencies { get; } = new();
    public Expression? OriginalContext { get; set; }

    /// <summary>
    /// Expressions produced by resolving a paged field (UseOffsetPaging/UseConnectionPaging) in a filter -
    /// they are the field's underlying collection (the Page/Connection wrapper only exists in the GraphQL
    /// schema). Tracked so a following items/edges access can be treated as identity and totalItems/totalCount
    /// as Count(). See IdentityExpression.
    /// </summary>
    public HashSet<Expression> PagedFieldExpressions { get; } = new();
}

using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery;

public class EqlCompileContext : CompileContext
{
    public EqlCompileContext(CompileContext compileContext)
        : base(compileContext.ExecutionOptions, compileContext.BulkData, compileContext.RequestContext, compileContext.CancellationToken) { }

    public List<IField> ServiceFieldDependencies { get; } = new();
    public Expression? OriginalContext { get; set; }
}

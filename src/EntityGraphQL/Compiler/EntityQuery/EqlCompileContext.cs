using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;
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

    /// <summary>
    /// The schema type of expressions in the filter whose dotnet type can not say which it is - several
    /// schema types sharing one dotnet type, e.g. property bags (Dictionary&lt;string, object&gt;), where each
    /// field names its type with Returns("Type"). Set for the filter's context from the field being
    /// filtered, then for each field and collection method the filter goes through.
    /// </summary>
    internal Dictionary<Expression, ISchemaType> SchemaTypes { get; } = new();

    /// <summary>
    /// Records that an expression - or, for a collection, its elements - is of a schema type. Only when
    /// the schema type's dotnet type is the expression's (or its elements'): a paged field's schema type
    /// is its Page/Connection wrapper while the filter works on the collection beneath it, and that is
    /// left to the lookup by dotnet type, as before.
    /// </summary>
    internal void RecordSchemaType(Expression expression, ISchemaType schemaType)
    {
        var dotnetType = schemaType.TypeDotnet;
        if (dotnetType.IsAssignableFrom(expression.Type) || (expression.Type.GetEnumerableOrArrayType() is { } elementType && dotnetType.IsAssignableFrom(elementType)))
            SchemaTypes[expression] = schemaType;
    }

    /// <summary>The schema type of an expression in the filter: as recorded, else by its dotnet type.</summary>
    internal ISchemaType SchemaTypeOf(Expression expression, ISchemaProvider schema, QueryRequestContext? requestContext) =>
        SchemaTypes.TryGetValue(expression, out var schemaType) ? schemaType : schema.GetSchemaType(expression.Type, false, requestContext);
}

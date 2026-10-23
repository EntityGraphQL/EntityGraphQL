using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// The schema type of each expression the filter goes through - the context it starts from, each field
    /// it reads (the schema type the field returns) and each collection method - and whether that is the
    /// expression's own type or its elements'. Kept because the dotnet type can not always say: several
    /// schema types share one (property bags, Dictionary&lt;string, object&gt;), and a field's expression may be
    /// less specific than what it returns (object, IDictionary, a cast).
    /// </summary>
    internal Dictionary<Expression, (ISchemaType SchemaType, bool IsList)> SchemaTypes { get; } = new();

    /// <summary>Records the schema type of an expression - or, for a list, of its elements.</summary>
    internal void RecordSchemaType(Expression expression, ISchemaType schemaType, bool isList)
    {
        // Only a type with fields can have one looked up on it - most field accesses in a filter are
        // scalars, and stop here.
        if (schemaType.IsScalar || schemaType.IsEnum)
            return;
        SchemaTypes[expression] = (schemaType, isList);
    }

    /// <summary>
    /// The schema type whose fields apply to an expression in the filter: as recorded - unless its dotnet type
    /// finds one more derived (a field declared as Person whose expression is an Employee is an Employee, with
    /// its fields) - else by its dotnet type, as before. Not a recorded list's: a field read straight off a list
    /// is the same error it always was.
    /// </summary>
    internal ISchemaType SchemaTypeOf(Expression expression, ISchemaProvider schema, QueryRequestContext? requestContext)
    {
        if (SchemaTypes.TryGetValue(expression, out var recorded) && !recorded.IsList && GqlTypeInfo.CastCompatible(expression.Type, recorded.SchemaType.TypeDotnet))
        {
            if (schema.TryGetSchemaType(expression.Type, false, out var byDotnetType, requestContext) && IsSubtype(byDotnetType!, recorded.SchemaType))
                return byDotnetType!;
            return recorded.SchemaType;
        }
        return schema.GetSchemaType(expression.Type, false, requestContext);
    }

    /// <summary>
    /// Whether a schema type is a subtype of another: one of its base types in the schema (a type implementing
    /// an interface), or over a subclass of its class (Employee : Person). Not just a dotnet type assignable to
    /// the other's - a Dictionary is an IDictionary, but a bag over one is no subtype of a bag over the other.
    /// </summary>
    private static bool IsSubtype(ISchemaType candidate, ISchemaType of) =>
        candidate != of
        && (candidate.BaseTypesReadOnly.Contains(of) || (of.TypeDotnet.IsClass && candidate.TypeDotnet.IsSubclassOf(of.TypeDotnet)));

    /// <summary>The schema type of a collection's elements, where it was recorded.</summary>
    internal ISchemaType? ElementSchemaTypeOf(Expression collection) =>
        SchemaTypes.TryGetValue(collection, out var recorded) && recorded.IsList ? recorded.SchemaType : null;
}

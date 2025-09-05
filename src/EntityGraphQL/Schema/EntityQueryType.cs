using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema;

public class EntityQueryType
{
    /// <summary>
    /// The compiler will end up setting this to the compiled lambda that can be used in LINQ functions
    /// </summary>
    /// <value></value>
    public LambdaExpression? Query { get; set; }

    /// <summary>
    /// Stores the raw filter text when compiled later (e.g. inside field extension with an active CompileContext)
    /// </summary>
    public string? Text { get; set; }
    public bool HasValue => Query != null || !string.IsNullOrWhiteSpace(Text);
    public List<IField> ServiceFieldDependencies { get; set; } = new();
    public Expression? OriginalContext { get; set; }
}

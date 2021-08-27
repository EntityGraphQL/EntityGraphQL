using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Holds the BaseGraphQLField and it's compiled expressions (result from GetNodeExpression)
    /// </summary>
    public class CompiledField
    {
        public CompiledField(BaseGraphQLField field, Expression expression)
        {
            Field = field;
            Expression = expression;
        }

        public BaseGraphQLField Field { get; set; }
        public Expression Expression { get; set; }
    }
}
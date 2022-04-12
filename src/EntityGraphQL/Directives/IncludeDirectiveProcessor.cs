using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace EntityGraphQL.Directives
{
    public class IncludeDirectiveProcessor : DirectiveProcessor<IncludeArguments>
    {
        public override string Name { get => "include"; }
        public override string Description { get => "Directs the executor to include this field or fragment only when the `if` argument is true."; }
        public override Type GetArgumentsType()
        {
            return typeof(IncludeArguments);
        }

        public override Expression? ProcessExpression(Expression expression, object arguments)
        {
            if (((IncludeArguments)arguments).@if)
                return expression;
            return null;
        }
    }

    public class IncludeArguments
    {
        [Description("Included when true.")]
        public bool @if { get; set; }
    }
}
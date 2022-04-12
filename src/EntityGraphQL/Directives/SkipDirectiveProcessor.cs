using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace EntityGraphQL.Directives
{
    public class SkipDirectiveProcessor : DirectiveProcessor<SkipArguments>
    {
        public override string Name { get => "skip"; }
        public override string Description { get => "Directs the executor to skip this field or fragment when the `if` argument is true."; }
        public override Type GetArgumentsType()
        {
            return typeof(SkipArguments);
        }

        public override Expression? ProcessExpression(Expression expression, object arguments)
        {
            if (((SkipArguments)arguments).@if)
                return null;
            return expression;
        }
    }

    public class SkipArguments
    {
        [Description("Excluded when true.")]
        public bool @if { get; set; }
    }
}
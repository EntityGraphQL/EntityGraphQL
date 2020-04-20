using System;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Directives
{
    public class FormatDateDirective : DirectiveProcessor<FormatDate>
    {
        public override bool ProcessesResult { get => true; }
        public override Type GetArgumentsType()
        {
            return typeof(FormatDate);
        }

        public override object ProcessResult(object value, FormatDate arguments)
        {
            if (value.GetType() == typeof(DateTime))
            {
                return ((DateTime)value).ToString(arguments.@as);
            }
            return value;
        }
    }

    public class FormatDate
    {
        public string @as { get; set; }
    }
}
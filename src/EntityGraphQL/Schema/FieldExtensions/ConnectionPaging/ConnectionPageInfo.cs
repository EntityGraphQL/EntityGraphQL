using System;
using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class ConnectionPageInfo
    {
        private readonly int totalCount;
        private readonly dynamic arguments;

        public ConnectionPageInfo(int totalCount, dynamic arguments)
        {
            this.totalCount = totalCount;
            this.arguments = arguments;
        }

        [GraphQLNotNull]
        [Description("Last cursor in the page. Use this as the next from argument")]
        public string EndCursor
        {
            get
            {
                var idx = totalCount;
                if (arguments.AfterNum != null && arguments.First != null)
                    idx = Math.Min(totalCount, arguments.AfterNum + arguments.First);
                else if (arguments.First != null)
                    idx = arguments.First;
                else if (arguments.BeforeNum != null)
                    idx = arguments.BeforeNum - 1;

                return ConnectionHelper.SerializeCursor(idx);
            }
        }

        [GraphQLNotNull]
        [Description("Start cursor in the page. Use this to go backwards with the before argument")]
        public string StartCursor
        {
            get
            {
                var idx = 1;
                if (arguments.AfterNum != null)
                    idx = arguments.AfterNum + 1;
                else if (arguments.Last != null)
                    idx = Math.Max((arguments.BeforeNum ?? (totalCount + 1)) - arguments.Last, 1);
                return ConnectionHelper.SerializeCursor(idx);
            }
        }

        [Description("If there is more data after this page")]
        public bool HasNextPage => arguments.First != null ?
            ((arguments.AfterNum ?? 0) + arguments.First) < totalCount :
            arguments.BeforeNum < totalCount;

        [Description("If there is data previous to this page")]
        public bool HasPreviousPage
        {
            get
            {
                return (arguments.AfterNum ?? 0) > 0 || (arguments.BeforeNum ?? totalCount) - (arguments.Last ?? totalCount) > 1;
            }
        }
    }
}
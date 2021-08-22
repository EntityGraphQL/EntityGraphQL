using System;
using System.ComponentModel;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema.Connections
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
                if (arguments.afterNum != null && arguments.first != null)
                    idx = Math.Min(totalCount, arguments.afterNum + arguments.first);
                else if (arguments.first != null)
                    idx = arguments.first;
                else if (arguments.beforeNum != null)
                    idx = arguments.beforeNum - 1;

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
                if (arguments.afterNum != null)
                    idx = arguments.afterNum + 1;
                else if (arguments.last != null)
                    idx = Math.Max((arguments.beforeNum ?? (totalCount + 1)) - arguments.last, 1);
                return ConnectionHelper.SerializeCursor(idx);
            }
        }

        [Description("If there is more data after this page")]
        public bool HasNextPage => arguments.first != null ?
            ((arguments.afterNum ?? 0) + arguments.first) < totalCount :
            arguments.beforeNum < totalCount;

        [Description("If there is data previous to this page")]
        public bool HasPreviousPage
        {
            get
            {
                return (arguments.afterNum ?? 0) > 0 || (arguments.beforeNum ?? totalCount) - (arguments.last ?? totalCount) > 1;
            }
        }
    }
}
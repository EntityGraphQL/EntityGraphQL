using System;
using System.ComponentModel;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema.Connections
{
    public class ConnectionPageInfo
    {
        private readonly int? afterNum;
        private readonly int? beforeNum;
        private readonly int totalCount;
        private readonly dynamic arguments;

        public ConnectionPageInfo(int totalCount, dynamic arguments)
        {
            this.totalCount = totalCount;
            this.arguments = arguments;

            if (arguments.first != null)
                afterNum = arguments.afterNum;
            else
                beforeNum = arguments.beforeNum;
        }

        [GraphQLNotNull]
        [Description("Last cursor in the page. Use this as the next from argument")]
        public string EndCursor => arguments.first != null ?
            ConnectionPagingExtension.SerializeCursor(Math.Min(arguments.first, totalCount - afterNum ?? 0), afterNum) :
            ConnectionPagingExtension.SerializeCursor(0, beforeNum - 1);

        [GraphQLNotNull]
        [Description("Start cursor in the page. Use this to go backwards with the before argument")]
        public string StartCursor => arguments.first != null ?
            ConnectionPagingExtension.SerializeCursor(afterNum ?? 0, 1) :
            ConnectionPagingExtension.SerializeCursor(beforeNum ?? 0, -(arguments.last ?? 0));

        [Description("If there is more data after this page")]
        public bool HasNextPage => arguments.first != null ?
            ((afterNum ?? 0) + arguments.first) < totalCount :
            beforeNum < totalCount;

        [Description("If there is data previous to this page")]
        public bool HasPreviousPage => arguments.first != null ?
            (afterNum ?? 0) > 0 :
            beforeNum - 1 - arguments.last > 0;
    }
}
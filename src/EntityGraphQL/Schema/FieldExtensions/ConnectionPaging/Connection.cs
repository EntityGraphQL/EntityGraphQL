using System.Collections.Generic;
using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions;

public class Connection<TEntity>(dynamic arguments)
{
    private readonly dynamic arguments = arguments;

    [Description("Edge information about each node in the collection")]
    public IEnumerable<ConnectionEdge<TEntity>> Edges { get; set; } = new List<ConnectionEdge<TEntity>>();

    private int totalCount;

    [Description("Total count of items in the collection")]
    public int TotalCount
    {
        get => totalCount;
        set
        {
            totalCount = value;
            // Store in arguments for cursor/skip calculations when using 'last' argument
            arguments.TotalCount = value;
        }
    }

    // Lazy PageInfo - only create when accessed/needed
    private ConnectionPageInfo? pageInfo;

    [Description("Information about this page of data")]
    public ConnectionPageInfo PageInfo
    {
        get => pageInfo ??= new ConnectionPageInfo(TotalCount, arguments);
        set => pageInfo = value;
    }
}

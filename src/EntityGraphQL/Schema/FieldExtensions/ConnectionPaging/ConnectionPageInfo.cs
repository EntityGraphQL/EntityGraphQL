using System;
using System.ComponentModel;

namespace EntityGraphQL.Schema.FieldExtensions;

public class ConnectionPageInfo
{
    private readonly int totalCount;
    private readonly dynamic arguments;
    private readonly bool? hasNextPageOverride;

    public ConnectionPageInfo(int totalCount, dynamic arguments)
        : this(totalCount, (object)arguments, null) { }

    public ConnectionPageInfo(int totalCount, dynamic arguments, bool? hasNextPageOverride)
    {
        this.totalCount = totalCount;
        this.arguments = arguments;
        this.hasNextPageOverride = hasNextPageOverride;
    }

    /// <summary>
    /// The before cursor, counting back from just past the end when it is past the end of this collection - `last: 3,
    /// before: 5` of 3 items is all 3. Per instance: a nested collection's parents share the arguments but not the count
    /// </summary>
    private int? BeforeNum => arguments.BeforeNum == null ? null : Math.Min((int)arguments.BeforeNum, totalCount + 1);

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
            else if (BeforeNum != null)
                idx = BeforeNum.Value - 1;

            return ConnectionHelper.SerializeCursor(idx);
        }
    }

    [Description("Start cursor in the page. Use this to go backwards with the before argument")]
    public string StartCursor
    {
        get
        {
            int idx = 1;
            if (arguments.AfterNum != null)
                idx = arguments.AfterNum + 1;
            else if (arguments.Last != null)
                idx = Math.Max((BeforeNum ?? (totalCount + 1)) - arguments.Last, 1);
            return ConnectionHelper.SerializeCursor(idx);
        }
    }

    [Description("If there is more data after this page")]
    public bool HasNextPage => hasNextPageOverride ?? (arguments.First != null ? ((arguments.AfterNum ?? 0) + arguments.First) < totalCount : BeforeNum <= totalCount);

    [Description("If there is data previous to this page")]
    // without before, last counts back from just past the end - as StartCursor
    public bool HasPreviousPage => (arguments.AfterNum ?? 0) > 0 || (BeforeNum ?? (totalCount + 1)) - (arguments.Last ?? totalCount) > 1;
}

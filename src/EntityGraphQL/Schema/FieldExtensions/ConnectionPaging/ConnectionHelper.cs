using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace EntityGraphQL.Schema.FieldExtensions;

public static class ConnectionHelper
{
    /// <summary>
    /// Serialize an index/row number into base64
    /// </summary>
    /// <returns></returns>
    public static unsafe string SerializeCursor(int index)
    {
        // results in less allocations
        const int totalUtf8Bytes = 4 * (20 / 3);
        Span<byte> resultSpan = stackalloc byte[totalUtf8Bytes];
        if (!Utf8Formatter.TryFormat(index, resultSpan, out int writtenBytes))
            throw new ArithmeticException();

        if (OperationStatus.Done != Base64.EncodeToUtf8InPlace(resultSpan, writtenBytes, out writtenBytes))
            throw new ArithmeticException();

        fixed (byte* bytePtr = resultSpan)
        {
            var base64String = Encoding.UTF8.GetString(bytePtr, writtenBytes);
            return base64String;
        }
    }

    /// <summary>
    /// Deserialize a base64 string index/row number into a an int
    /// </summary>
    /// <param name="after"></param>
    /// <returns></returns>
    public static unsafe int? DeserializeCursor(ReadOnlySpan<char> after)
    {
        if (after.IsEmpty)
            return null;

        fixed (char* charPtr = after)
        {
            var count = Encoding.UTF8.GetByteCount(charPtr, after.Length);

            Span<byte> buffer = stackalloc byte[count];

            fixed (byte* bytePtr = buffer)
            {
                Encoding.UTF8.GetBytes(charPtr, after.Length, bytePtr, buffer.Length);
            }

            if (OperationStatus.Done != Base64.DecodeFromUtf8InPlace(buffer, out int writtenBytes))
                throw new ArithmeticException();

            if (!Utf8Parser.TryParse(buffer[..writtenBytes], out int index, out _))
                throw new ArithmeticException();

            return index;
        }
    }

    /// <summary>
    /// Used at runtime in the expression built above
    /// </summary>
    public static string GetCursor(dynamic arguments, int idx, int? offset = null)
    {
        return SerializeCursor(GetCursorIndex(arguments, idx, offset));
    }

    /// <summary>
    /// The row index a cursor represents. Every branch is linear in idx, so the value at idx 0 is a per-request
    /// base the per-row cursors can be derived from (see <see cref="ApplyCursors{TEntity}"/>).
    /// </summary>
    public static int GetCursorIndex(dynamic arguments, int idx, int? offset = null)
    {
        var index = idx + 1;
        if (arguments.AfterNum != null)
            index += arguments.AfterNum;
        if (arguments.Last != null)
        {
            if (arguments.BeforeNum != null)
                index = arguments.BeforeNum - arguments.Last + idx;
            else
                index += arguments.TotalCount - (arguments.Last ?? 0);
        }

        if (offset < 0)
            index = idx + 1;

        return index;
    }

    /// <summary>
    /// Used at runtime: enumerates the page (executing the DB query for an IQueryable source) and assigns each
    /// edge its cursor. The dynamic argument math runs once per request here, not once per row.
    /// </summary>
    public static System.Collections.Generic.IEnumerable<ConnectionEdge<TEntity>> ApplyCursors<TEntity>(System.Collections.Generic.IEnumerable<ConnectionEdge<TEntity>> edges, dynamic arguments)
    {
        int? offset = GetSkipNumber(arguments, false);
        int baseIndex = GetCursorIndex(arguments, 0, offset);
        var i = 0;
        foreach (var edge in edges)
        {
            edge.Cursor = SerializeCursor(baseIndex + i++);
            yield return edge;
        }
    }

    private static readonly ConcurrentDictionary<(Type, string), FieldInfo[]> cursorFieldsCache = new();

    /// <summary>
    /// Used at runtime for a nested collection: sets the cursor fields of the already-projected edges. The edges are
    /// rows skip + 1, skip + 2, ... of the collection. cursorFields is a comma separated list of field names.
    ///
    /// reversedFromCount is set when the page was taken in reverse (last): the edges are the last of that many rows,
    /// newest first. They are put back in order and skip is `after` - any rows up to it are dropped
    /// </summary>
    public static List<TEdge>? SetCursors<TEdge>(List<TEdge>? edges, int skip, int? reversedFromCount, string cursorFields)
    {
        if (edges == null)
            return null;
        if (reversedFromCount != null)
        {
            edges.Reverse();
            var rowsBefore = reversedFromCount.Value - edges.Count;
            if (rowsBefore < skip)
            {
                edges.RemoveRange(0, Math.Min(skip - rowsBefore, edges.Count));
                rowsBefore = skip;
            }
            skip = rowsBefore;
        }
        var fields = cursorFieldsCache.GetOrAdd((typeof(TEdge), cursorFields), key => Array.ConvertAll(key.Item2.Split(','), name => key.Item1.GetField(name)!));
        for (var i = 0; i < edges.Count; i++)
        {
            var cursor = SerializeCursor(skip + i + 1);
            foreach (var field in fields)
                field.SetValue(edges[i], cursor);
        }
        return edges;
    }

    /// <summary>
    /// Used at runtime: the row number of the after cursor, 0 if none
    /// </summary>
    public static int GetAfterNumber(dynamic arguments)
    {
        int? after = arguments.AfterNum;
        return after ?? 0;
    }

    /// <summary>
    /// Used at runtime in the expression built above
    /// </summary>
    public static int? GetSkipNumber(dynamic arguments, bool fixNegativeOffset = true)
    {
        if (arguments.AfterNum != null)
            return arguments.AfterNum;
        if (arguments.Last != null)
        {
            var c = ((arguments.BeforeNum - 1) ?? arguments.TotalCount) - arguments.Last;

            // Enumerable.Skip does not accept negative numbers.
            if (fixNegativeOffset)
                c = c > 0 ? c : 0;
            return c;
        }
        return 0;
    }

    /// <summary>
    /// Used at runtime: answers pageInfo.hasNextPage with a cheap EXISTS query (skip past the current page,
    /// Any()) instead of a full COUNT when nothing else in the selection needs the total. Only used for
    /// forward paging (first/after) - see ConnectionPagingExtension.
    /// </summary>
    public static bool PageHasNext<TSource>(System.Linq.IQueryable<TSource> source, dynamic arguments)
    {
        int? skip = GetHasNextSkip(arguments);
        if (skip == null)
            return false; // no page size = the whole remaining collection was returned
        return System.Linq.Queryable.Any(System.Linq.Queryable.Skip(source, skip.Value));
    }

    /// <summary>
    /// Used at runtime: how many items to skip to check for an item past the current page (forward paging), null when
    /// there is no page size so the whole remaining collection was returned
    /// </summary>
    public static int? GetHasNextSkip(dynamic arguments)
    {
        int? first = arguments.First;
        if (first == null)
            return null;
        string? after = arguments.After;
        return (DeserializeCursor(after) ?? 0) + first.Value;
    }

    /// <summary>
    /// Used at runtime - see the IQueryable overload.
    /// </summary>
    public static bool PageHasNext<TSource>(System.Collections.Generic.IEnumerable<TSource> source, dynamic arguments)
    {
        int? first = arguments.First;
        if (first == null)
            return false;
        string? after = arguments.After;
        int skip = (DeserializeCursor(after) ?? 0) + first.Value;
        return System.Linq.Enumerable.Any(System.Linq.Enumerable.Skip(source, skip));
    }

    /// <summary>
    /// Used at runtime in the expression built above
    /// </summary>
    public static int? GetTakeNumber(dynamic arguments, int? offset = 0)
    {
        if (arguments.First == null && arguments.Last == null && arguments.BeforeNum == null || offset == null)
            return null;

        // In cases where we have Last > BeforeNum, we need to take fewer results than Last says to
        // See SkipTakeTests.TestLastAndBefore_WhenLastGreaterThanBeforeNum
        var offsetAdjustedLast = offset >= 0 ? arguments.Last : arguments.Last + offset;
        return arguments.First ?? offsetAdjustedLast ?? (arguments.BeforeNum - 1);
    }
}

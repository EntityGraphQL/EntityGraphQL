using System;
using System.Buffers;
using System.Buffers.Text;
using System.Text;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class ConnectionHelper
    {
        /// <summary>
        /// Serialize an index/row number into base64
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        public static unsafe string SerializeCursor(int index)
        {
            // resuts in less allocations
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
        public static unsafe int? DeserializeCursor(string after)
        {
            if (string.IsNullOrEmpty(after))
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

                if (!Utf8Parser.TryParse(buffer.Slice(0, writtenBytes), out int index, out _))
                    throw new ArithmeticException();

                return index;
            }
        }


        /// <summary>
        /// Used at runtime in the expression built above
        /// </summary>
        public static string GetCursor(dynamic arguments, int idx)
        {
            var index = idx + 1;
            if (arguments.afterNum != null)
                index += arguments.afterNum;
            if (arguments.last != null)
            {
                if (arguments.beforeNum != null)
                    index = arguments.beforeNum - arguments.last + idx;
                else
                    index += arguments.totalCount - (arguments.last ?? 0);
            }
            return ConnectionHelper.SerializeCursor(index);
        }
        /// <summary>
        /// Used at runtime in the expression built above
        /// </summary>
        public static int? GetSkipNumber(dynamic arguments)
        {
            if (arguments.afterNum != null)
                return arguments.afterNum;
            if (arguments.last != null)
                return (arguments.beforeNum ?? arguments.totalCount) - arguments.last;
            return 0;
        }
        /// <summary>
        /// Used at runtime in the expression built above
        /// </summary>
        public static int? GetTakeNumber(dynamic arguments)
        {
            if (arguments.first == null && arguments.last == null && arguments.beforeNum == null)
                return null;
            return arguments.first ?? arguments.last ?? (arguments.beforeNum - 1);
        }
    }
}
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.Connections;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseConnectionPagingExtension
    {
        /// <summary>
        /// Update field to implement paging with the Connection<> classes and metadata.
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static Field UseConnectionPaging(this Field field)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"UseConnectionPaging must only be called on a field that returns an IEnumerable");
            field.AddExtension(new ConnectionPagingExtension());
            return field;
        }
    }

    /// <summary>
    /// Sets up a few extensions to modify a simple collection expression - db.Movies.OrderBy() into a connection paging graph
    /// </summary>
    public class ConnectionPagingExtension : BaseFieldExtension
    {
        private MethodCallExpression originalEdgeExpression;
        private Field edgesField;
        private ConnectionEdgeExtension edgesExtension;
        private ParameterExpression tmpArgParam;
        public MethodCallExpression EdgeExpression { get; internal set; }

        /// <summary>
        /// Configure the field for a connection style paging field. Do as much as we can here as it is only executed once.
        ///
        /// There are a few fun things happening.
        ///
        /// 1. In this extension we set up the field with the Connection<T> object graph using the constructor to implement most
        ///    of the fields
        /// 2. We set up an extension on this field.edges.node to capture the selection from the compiled query as node is the <T>
        ///    they are selecting fields from
        /// 3. We set up an extension of field.edges which using data from this extension (we get the context and the args) and the
        ///    field.edges.node Select() to build a EF compatible expression that only returns the fields asked for in edges.node
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="field"></param>
        public override void Configure(ISchemaProvider schema, Field field)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use ConnectionPagingExtension. Found type {field.ReturnType.TypeDotnet}");

            // Make sure required types are in the schema
            if (!schema.HasType("PageInfo"))
                schema.AddType(typeof(ConnectionPageInfo), "PageInfo", "Metadata about a page of data").AddAllFields();
            var edgeName = $"{field.ReturnType.SchemaType.Name}Edge";
            Type listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType();

            if (!schema.HasType(edgeName))
            {
                var edgeType = typeof(ConnectionEdge<>).MakeGenericType(listType);
                schema.AddType(edgeType, edgeName, "Metadata about an edge of page result").AddAllFields();
            }

            ISchemaType returnSchemaType;
            var connectionName = $"{field.ReturnType.SchemaType.Name}Connection";
            if (!schema.HasType(connectionName))
            {
                var type = typeof(Connection<>)
                    .MakeGenericType(listType);
                returnSchemaType = schema.AddType(type, connectionName, $"Metadata about a {field.ReturnType.SchemaType.Name} connection (paging over people)").AddAllFields();
            }
            else
            {
                returnSchemaType = schema.Type(connectionName);
            }
            var returnType = returnSchemaType.TypeDotnet;

            field.UpdateReturnType(SchemaBuilder.MakeGraphQlType(schema, returnType, connectionName));

            // Update field arguments
            field.AddArguments(new ConnectionArgs());

            // Rebuild expression so all the fields and types are known
            // and get it ready for completion at runtime (we need to know the selection fields to complete)
            // it is built to reduce redundant repeated expressions. The whole thing ends up in a null check wrap
            // conceptually it does similar to below (using Demo context)
            // See Connection for implemention details of TotalCount and PageInfo
            // (db, arguments) => {
            //      var connection = new Connection<Person>(db.Actors.Select(a => a.Person).Count(), arguments)
            //      {
            //          Edges = db.Actors.Select(a => a.Person).OrderBy(a => a.Id)
            //              .Skip(GetSkipNumber(arguments))
            //              .Take(GetTakeNumber(arguments))
            //              <----- we insert Select() here so that we do not fetch the whole table if using EF
            //              .Select((a, idx) => new ConnectionEdge<Person> // this is from Enumerable and EF will run the above
            //              {
            //                  Node = a,
            //                  Cursor = GetCursor(arguments, idx),
            //              })
            //      };
            //      if (connection == null)
            //          return null;
            //      return .... // does the select of only the Conneciton fields asked for
            tmpArgParam = Expression.Parameter(field.ArgumentsType, "tmp_argParam");

            var totalCountExp = Expression.Call(typeof(Queryable), "Count", new Type[] { listType }, field.Resolve);

            var selectParam = Expression.Parameter(listType);
            originalEdgeExpression = Expression.Call(typeof(QueryableExtensions), "Take", new Type[] { listType },
                Expression.Call(typeof(QueryableExtensions), "Skip", new Type[] { listType },
                    field.Resolve,
                    Expression.Call(typeof(ConnectionPagingExtension), "GetSkipNumber", null, tmpArgParam)
                ),
                Expression.Call(typeof(ConnectionPagingExtension), "GetTakeNumber", null, tmpArgParam)
            );

            // set up Extension on Edges.Node field to handle the Select() insertion
            edgesField = returnSchemaType.GetField("edges", null);

            // We use this extension to update the Edges context by inserting the Select() which we get from the above extension
            edgesExtension = new ConnectionEdgeExtension(this, listType, selectParam);
            edgesField.AddExtension(edgesExtension);

            // We use this extension to "steal" the node selection
            var nodeExtension = new ConnectionEdgeNodeExtension(edgesExtension, selectParam);
            edgesField.ReturnType.SchemaType.GetField("node", null).AddExtension(nodeExtension);

            // totalCountExp gets executed once in the new Connection() {} and we can reuse it
            var expression = Expression.MemberInit(Expression.New(returnType.GetConstructor(new[] { totalCountExp.Type, tmpArgParam.Type }), totalCountExp, tmpArgParam));

            field.UpdateExpression(expression);
        }

        public override Expression GetExpression(Field field, ExpressionResult expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            // Here we now have the original context needed in our edges expression to use in the sub fields
            EdgeExpression = (MethodCallExpression)parameterReplacer.Replace(
                parameterReplacer.Replace(originalEdgeExpression, field.FieldParam, context),
                tmpArgParam,
                argExpression
            );

            // deserialize cursors here once (not many times in the fields)
            arguments.afterNum = DeserializeCursor(arguments.after);
            arguments.beforeNum = DeserializeCursor(arguments.before);

            // we get the arguments at this level but need to use them on the edge field
            edgesExtension.ArgExpression = argExpression;

            return expression;
        }

        /// <summary>
        /// Used at runtime in the expression built above
        /// </summary>
        public static string GetCursor(dynamic arguments, int idx)
        {
            return SerializeCursor(idx + 1, !string.IsNullOrEmpty(arguments.after) ? arguments.afterNum : arguments.beforeNum - arguments.last - 1);
        }
        /// <summary>
        /// Used at runtime in the expression built above
        /// </summary>
        public static int? GetSkipNumber(dynamic arguments)
        {
            return arguments.afterNum ?? (!string.IsNullOrEmpty(arguments.before) ? arguments.beforeNum - 1 - (arguments.last ?? 0) : 0);
        }
        /// <summary>
        /// Used at runtime in the expression built above
        /// </summary>
        public static int? GetTakeNumber(dynamic arguments)
        {
            if (arguments.first == null && arguments.last == null && arguments.beforeNum == null)
                return null;
            return arguments.first ?? Math.Min(arguments.last ?? int.MaxValue, arguments.beforeNum - 1);
        }
        /// <summary>
        /// Serialize an index/row number into base64
        /// </summary>
        /// <param name="idx"></param>
        /// <param name="from"></param>
        /// <returns></returns>
        public static unsafe string SerializeCursor(int idx, int? from)
        {
            // resuts in less allocations
            const int totalUtf8Bytes = 4 * (20 / 3);
            var index = from != null ? from.Value + idx : idx;
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
    }
}
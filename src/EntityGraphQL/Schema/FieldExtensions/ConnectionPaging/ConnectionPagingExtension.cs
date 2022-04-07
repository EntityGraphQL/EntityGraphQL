using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    /// <summary>
    /// Sets up a few extensions to modify a simple collection expression - db.Movies.OrderBy() into a connection paging graph
    /// </summary>
    public class ConnectionPagingExtension : BaseFieldExtension
    {
        private readonly int? defaultPageSize;
        private readonly int? maxPageSize;
        private Field edgesField;
        private Type listType;
        private bool isQueryable;
        private Type returnType;
        private List<IFieldExtension> extensionsBeforePaging;

        public ConnectionPagingExtension(int? defaultPageSize, int? maxPageSize)
        {
            this.defaultPageSize = defaultPageSize;
            this.maxPageSize = maxPageSize;
        }

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
            listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType();
            isQueryable = typeof(IQueryable).IsAssignableFrom(field.ReturnType.TypeDotnet);

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
            returnType = returnSchemaType.TypeDotnet;

            field.UpdateReturnType(SchemaBuilder.MakeGraphQlType(schema, returnType, connectionName));

            // Update field arguments
            field.AddArguments(new ConnectionArgs());

            // set up Extension on Edges.Node field to handle the Select() insertion
            edgesField = returnSchemaType.GetField(schema.SchemaFieldNamer("Edges"), null);
            // move expression
            // This is the original expression that was defined in the schema - the collection
            // UseConnectionPaging() basically moves it to originalField.edges
            edgesField.UpdateExpression(field.Resolve);
            // We steal any previous extensions as they were expected to work on the original Resolve which we moved to Edges
            extensionsBeforePaging = field.Extensions.Take(field.Extensions.FindIndex(e => e is ConnectionPagingExtension)).ToList();
            // the remaining extensions expect to be built from the ConnectionPaging shape
            field.Extensions = field.Extensions.Skip(extensionsBeforePaging.Count).ToList();

            // We use this extension to update the Edges context by inserting the Select() which we get from the above extension
            var edgesExtension = new ConnectionEdgeExtension(listType, isQueryable, field.ArgumentParam, field.ArgumentParam, extensionsBeforePaging);
            edgesField.AddExtension(edgesExtension);
            // Move the arguments definition to the Edges field as it needs them for processing
            // don't push field.FieldParam over as we rebuild the field from the parent context
            edgesField.ArgumentsType = field.ArgumentsType;
            edgesField.ArgumentParam = field.ArgumentParam;
            edgesField.Arguments = field.Arguments;
            edgesField.ArgumentsAreInternal = true;

            // Rebuild expression so all the fields and types are known
            // and get it ready for completion at runtime (we need to know the selection fields to complete)
            // it is built to reduce redundant repeated expressions. The whole thing ends up in a null check wrap
            // conceptually it does similar to below (using Demo context)
            // See Connection for implemention details of TotalCount and PageInfo
            // (ctx, arguments) => {
            //      var connection = new Connection<Person>(ctx.Actors.Select(a => a.Person)
            //              -- other extensions might do thigns here (e.g. filter / sort)
            //             .Count(), arguments)
            //      {
            //          Edges = ctx.Actors.Select(a => a.Person)
            //              -- other extensions might do thigns here (e.g. filter / sort)
            //              .Skip(GetSkipNumber(arguments))
            //              .Take(GetTakeNumber(arguments))
            //              // we insert Select() here so that we do not fetch the whole table if using EF
            //              .Select(a => new ConnectionEdge<Person>
            //              {
            //                  Node = new {
            //                      field1 = a.field1,
            //                      ...
            //                 },
            //                 Cursor = null // built below
            //              })
            //              // this is the select in memory that lets us build the cursors
            //              .Select((a, idx) => new ConnectionEdge<Person> // this is from Enumerable and EF will run the above
            //              {
            //                  Node = a,
            //                  Cursor = ConnectionHelper.GetCursor(arguments, idx)
            //              }),
            //      };
            //      if (connection == null)
            //          return null;
            //      return .... // does the select of only the Conneciton fields asked for
            // need to set this up here as the types are needed as we visiting the query tree
            // we build the real one below in GetExpression()
            var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", new Type[] { listType }, edgesField.Resolve);
            var fieldExpression = Expression.MemberInit(Expression.New(returnType.GetConstructor(new[] { totalCountExp.Type, field.ArgumentParam.Type }), totalCountExp, field.ArgumentParam));
            field.UpdateExpression(fieldExpression);
        }

        public override Expression GetExpression(Field field, Expression expression, ParameterExpression argExpression, dynamic arguments, Expression context, bool servicesPass, ParameterReplacer parameterReplacer)
        {
            // second pass with services we have the new edges shape. We need to handle things on the EdgeExtension
            if (servicesPass)
                return expression;

            // check and set up arguments
            if (arguments.Before != null && arguments.After != null)
                throw new ArgumentException($"Field only supports either before or after being supplied, not both.");
            if (arguments.First != null && arguments.First < 0)
                throw new ArgumentException($"first argument can not be less than 0.");
            if (arguments.Last != null && arguments.Last < 0)
                throw new ArgumentException($"last argument can not be less than 0.");

            if (maxPageSize.HasValue)
            {
                if (arguments.First != null && arguments.First > maxPageSize.Value)
                    throw new ArgumentException($"first argument can not be greater than {maxPageSize.Value}.");
                if (arguments.Last != null && arguments.Last > maxPageSize.Value)
                    throw new ArgumentException($"last argument can not be greater than {maxPageSize.Value}.");
            }

            if (arguments.First == null && arguments.Last == null && defaultPageSize != null)
                arguments.First = defaultPageSize;

            // deserialize cursors here once (not many times in the fields)
            arguments.AfterNum = ConnectionHelper.DeserializeCursor(arguments.After);
            arguments.BeforeNum = ConnectionHelper.DeserializeCursor(arguments.Before);


            // totalCountExp gets executed once in the new Connection() {} and we can reuse it
            var edgeExpression = edgesField.Resolve;

            if (edgesField.Extensions.Any())
            {
                // if we have other extensions (filter etc) we need to apply them to the totalCount
                foreach (var extension in extensionsBeforePaging)
                {
                    edgeExpression = extension.GetExpression(edgesField, edgeExpression, argExpression, arguments, context, servicesPass, parameterReplacer);
                }
            }
            var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", new Type[] { listType }, edgeExpression);
            expression = Expression.MemberInit(Expression.New(returnType.GetConstructor(new[] { totalCountExp.Type, field.ArgumentParam.Type }), totalCountExp, field.ArgumentParam));

            return expression;
        }
    }
}
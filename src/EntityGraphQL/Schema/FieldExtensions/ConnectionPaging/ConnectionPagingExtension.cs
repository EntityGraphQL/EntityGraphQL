using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
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
        private IField? edgesField;
        private Type? listType;
        private bool isQueryable;
        private Type? returnType;
        private List<IFieldExtension> extensionsBeforePaging = new();

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
        public override void Configure(ISchemaProvider schema, IField field)
        {
            if (field.ResolveExpression == null)
                throw new EntityGraphQLCompilerException($"ConnectionPagingExtension requires a Resolve function set on the field");

            if (!field.ResolveExpression.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use ConnectionPagingExtension. Found type {field.ReturnType.TypeDotnet}");

            // Make sure required types are in the schema
            if (!schema.HasType(typeof(ConnectionPageInfo)))
                schema.AddType(typeof(ConnectionPageInfo), "PageInfo", "Metadata about a page of data").AddAllFields();
            listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()!;
            isQueryable = typeof(IQueryable).IsAssignableFrom(field.ReturnType.TypeDotnet);

            var edgeType = typeof(ConnectionEdge<>).MakeGenericType(listType);
            if (!schema.HasType(edgeType))
            {
                var edgeName = $"{field.ReturnType.SchemaType.Name}Edge";
                schema.AddType(edgeType, edgeName, "Metadata about an edge of page result").AddAllFields();
            }

            ISchemaType returnSchemaType;
            var connectionType = typeof(Connection<>).MakeGenericType(listType);
            var connectionName = $"{field.ReturnType.SchemaType.Name}Connection";
            if (!schema.HasType(connectionType))
            {
                returnSchemaType = schema.AddType(connectionType, connectionName, $"Metadata about a {field.ReturnType.SchemaType.Name} connection (paging over people)").AddAllFields();
            }
            else
            {
                returnSchemaType = schema.Type(connectionName);
            }
            returnType = returnSchemaType.TypeDotnet;

            field.Returns(SchemaBuilder.MakeGraphQlType(schema, returnType, connectionName));

            // Update field arguments
            field.AddArguments(new ConnectionArgs());

            // set up Extension on Edges.Node field to handle the Select() insertion
            edgesField = returnSchemaType.GetField(schema.SchemaFieldNamer("Edges"), null);
            // move expression
            // This is the original expression that was defined in the schema - the collection
            // UseConnectionPaging() basically moves it to originalField.edges
            edgesField.UpdateExpression(field.ResolveExpression);
            // We steal any previous extensions as they were expected to work on the original Resolve which we moved to Edges
            extensionsBeforePaging = field.Extensions.Take(field.Extensions.FindIndex(e => e is ConnectionPagingExtension)).ToList();
            // the remaining extensions expect to be built from the ConnectionPaging shape
            field.Extensions = field.Extensions.Skip(extensionsBeforePaging.Count).ToList();

            // We use this extension to update the Edges context by inserting the Select() which we get from the above extension
            var edgesExtension = new ConnectionEdgeExtension(listType, isQueryable, extensionsBeforePaging, field.FieldParam!, defaultPageSize, maxPageSize);
            edgesField.AddExtension(edgesExtension);
            // args on field get used in edges field we inject
            edgesField.UseArgumentsFrom(field);

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
            var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", new Type[] { listType }, edgesField.ResolveExpression!);
            var argTypes = new List<Type>
            {
                totalCountExp.Type,
                field.ArgumentsParameter!.Type
            };
            var paramsArgs = new List<Expression>
            {
                totalCountExp,
                field.ArgumentsParameter
            };
            var fieldExpression = Expression.MemberInit(Expression.New(returnType.GetConstructor(argTypes.ToArray())!, paramsArgs));

            field.UpdateExpression(fieldExpression);
        }

        public override Expression? GetExpression(IField field, Expression expression, ParameterExpression? argumentParam, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
        {
            // second pass with services we have the new edges shape. We need to handle things on the EdgeExtension
            if (servicesPass)
                return expression;

            if (argumentParam == null)
                throw new ArgumentNullException(nameof(argumentParam));

            // totalCountExp gets executed once in the new Connection() {} and we can reuse it
            var edgeExpression = edgesField!.ResolveExpression;

            if (edgesField.Extensions.Any())
            {
                // if we have other extensions (filter etc) we need to apply them to the totalCount
                foreach (var extension in extensionsBeforePaging)
                {
                    edgeExpression = extension.GetExpression(edgesField, edgeExpression, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer);
                }
            }
            var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", new Type[] { listType! }, edgeExpression!);
            expression = Expression.MemberInit(Expression.New(returnType!.GetConstructor(new[] { totalCountExp.Type, argumentParam.Type })!, totalCountExp, argumentParam));

            return expression;
        }
    }
}
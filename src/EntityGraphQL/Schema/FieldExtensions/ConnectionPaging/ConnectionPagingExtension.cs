using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Sets up a few extensions to modify a simple collection expression - db.Movies.OrderBy() into a connection paging graph
/// </summary>
public class ConnectionPagingExtension : BaseFieldExtension
{
    private Type? listType;
    private bool isQueryable;
    private Type? returnType;

    public Expression? OriginalFieldExpression { get; private set; }
    public int? DefaultPageSize { get; }
    public int? MaxPageSize { get; }
    public List<IFieldExtension> ExtensionsBeforePaging { get; private set; } = [];

    public ConnectionPagingExtension(int? defaultPageSize, int? maxPageSize)
    {
        DefaultPageSize = defaultPageSize;
        MaxPageSize = maxPageSize;
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
            schema.AddType<ConnectionPageInfo>("PageInfo", "Metadata about a page of data").AddAllFields();
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

        field.Returns(SchemaBuilder.MakeGraphQlType(schema, false, returnType, connectionName, field.Name, field.FromType));

        // Update field arguments
        field.AddArguments(new ConnectionArgs());

        // set up Extension on Edges.Node field to handle the Select() insertion
        var edgesField = returnSchemaType.GetField(schema.SchemaFieldNamer("Edges"), null);

        // We steal any previous extensions as they were expected to work on the original Resolve which we moved to Edges
        ExtensionsBeforePaging = field.Extensions.Take(field.Extensions.FindIndex(e => e is ConnectionPagingExtension)).ToList();
        // the remaining extensions expect to be built from the ConnectionPaging shape
        field.Extensions = field.Extensions.Skip(ExtensionsBeforePaging.Count).ToList();
        // We use this extension to update the Edges context by inserting the Select() which we get from the above extension
        // if they have 2 fields with the type and paging we don't want to add extension multiple times
        // See OffsetPagingTests.TestMultiUseWithArgs
        if (!edgesField.Extensions.Any(e => e is ConnectionEdgeExtension))
            edgesField.AddExtension(new ConnectionEdgeExtension(listType, isQueryable));

        OriginalFieldExpression = field.ResolveExpression;

        // Rebuild expression so all the fields and types are known
        // and get it ready for completion at runtime (we need to know the selection fields to complete)
        // it is built to reduce redundant repeated expressions. The whole thing ends up in a null check wrap
        // conceptually it does similar to below (using Demo context)
        // See Connection for implementation details of TotalCount and PageInfo
        // (ctx, arguments) => {
        //      var connection = new Connection<Person>(ctx.Actors.Select(a => a.Person)
        //              -- other extensions might do things here (e.g. filter / sort)
        //             .Count(), arguments)
        //      {
        //          Edges = ctx.Actors.Select(a => a.Person)
        //              -- other extensions might do things here (e.g. filter / sort)
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
        //      return .... // does the select of only the Connection fields asked for
        // need to set this up here as the types are needed as we visiting the query tree
        // we build the real one below in GetExpression()
        var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", [listType], OriginalFieldExpression!);
        var argTypes = new List<Type> { totalCountExp.Type, field.ArgumentsParameter!.Type };
        var paramsArgs = new List<Expression> { totalCountExp, field.ArgumentsParameter };
        var fieldExpression = Expression.MemberInit(Expression.New(returnType.GetConstructor(argTypes.ToArray())!, paramsArgs));

        field.UpdateExpression(fieldExpression);
    }

    public override Expression? GetExpression(
        IField field,
        Expression expression,
        ParameterExpression? argumentParam,
        dynamic? arguments,
        Expression context,
        IGraphQLNode? parentNode,
        bool servicesPass,
        ParameterReplacer parameterReplacer
    )
    {
        // second pass with services we have the new edges shape. We need to handle things on the EdgeExtension
        if (servicesPass)
            return expression;

#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(argumentParam, nameof(argumentParam));
#else
        if (argumentParam == null)
            throw new ArgumentNullException(nameof(argumentParam));
#endif

        // totalCountExp gets executed once in the new Connection() {} and we can reuse it
        var edgeExpression = OriginalFieldExpression!;

        if (ExtensionsBeforePaging.Count > 0)
        {
            // if we have other extensions (filter etc) we need to apply them to the totalCount
            foreach (var extension in ExtensionsBeforePaging)
            {
                edgeExpression = extension.GetExpression(field, edgeExpression, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer)!;
            }
        }
        var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), nameof(Enumerable.Count), [listType!], edgeExpression!);
        expression = Expression.MemberInit(Expression.New(returnType!.GetConstructor([totalCountExp.Type, argumentParam.Type])!, totalCountExp, argumentParam));

        return expression;
    }
}

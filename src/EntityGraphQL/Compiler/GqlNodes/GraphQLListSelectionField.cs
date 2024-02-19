using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents a field node in the GraphQL query. That operates on a list of things.
    /// query MyQuery {
    ///     people { # GraphQLListSelectionField
    ///         id, name
    ///     }
    ///     person(id: "") { id }
    /// }
    /// </summary>
    public class GraphQLListSelectionField : BaseGraphQLQueryField
    {
        public bool AllowToList { get; set; } = true;
        public Expression ListExpression { get; set; }
        public GraphQLCollectionToSingleField? ToSingleNode { get; set; }

        /// <summary>
        /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
        /// </summary>
        /// <param name="schema">The Schema Provider that defines the GraphQL schema</param>
        /// <param name="field">Field from the schema that this GraphQLListSelectionField is built from</param>
        /// <param name="name">Name of the field. Could be the alias that the user provided</param>
        /// <param name="nextFieldContext">A context for a field building on this. This will be the list element parameter</param>
        /// <param name="rootParameter">Root parameter used by this nodeExpression (movie in example above).</param>
        /// <param name="nodeExpression">Expression for the list</param>
        /// <param name="context">Partent node</param>
        /// <param name="arguments"></param>
        public GraphQLListSelectionField(ISchemaProvider schema, IField? field, string name, ParameterExpression? nextFieldContext, ParameterExpression? rootParameter, Expression nodeExpression, IGraphQLNode context, Dictionary<string, object>? arguments)
            : base(schema, field, name, nextFieldContext, rootParameter, context, arguments)
        {
            this.ListExpression = nodeExpression;
        }

        public GraphQLListSelectionField(GraphQLListSelectionField context, ParameterExpression? nextFieldContext)
           : base(context, nextFieldContext)
        {
            this.ListExpression = context.ListExpression;
            AllowToList = context.AllowToList;
        }

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        protected override Expression? GetFieldExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, List<Type>? possibleNextContextTypes, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            if (withoutServiceFields && isRoot && HasServices)
                return null;

            var listContext = HandleBulkServiceResolver(compileContext, withoutServiceFields, ListExpression)!;

            ParameterExpression? nextFieldContext = (ParameterExpression)NextFieldContext!;
            if (contextChanged && replacementNextFieldContext != null)
            {
                listContext = ReplaceContext(replacementNextFieldContext!, isRoot, replacer, listContext!, possibleNextContextTypes);
                nextFieldContext = Expression.Parameter(listContext.Type.GetEnumerableOrArrayType()!, $"{nextFieldContext.Name}2");
            }
            (listContext, var argumentParams) = Field?.GetExpression(listContext!, replacementNextFieldContext, ParentNode!, schemaContext, compileContext, Arguments, docParam, docVariables, Directives, contextChanged, replacer) ?? (ListExpression, null);
            if (listContext == null)
                return null;

            (listContext, var newNextFieldContext) = ProcessExtensionsPreSelection(listContext, nextFieldContext, replacer);
            if (newNextFieldContext != null)
                nextFieldContext = newNextFieldContext;

            var selectionFields = GetSelectionFields(compileContext, serviceProvider, fragments, docParam, docVariables, withoutServiceFields, nextFieldContext, schemaContext, contextChanged, replacer);

            if (selectionFields == null || selectionFields.Count == 0)
            {
                if (withoutServiceFields && HasServices)
                    return null;
                return listContext;
            }

            (listContext, selectionFields, nextFieldContext) = ProcessExtensionsSelection(listContext, selectionFields, nextFieldContext, argumentParams, contextChanged, replacer);

            if (HasServices)
                compileContext.AddServices(Field!.Services);

            Expression? resultExpression = null;
            if (!withoutServiceFields)
            {
                bool needsServiceWrap = NeedsServiceWrap(withoutServiceFields);
                if (needsServiceWrap)
                {
                    // To support a common use case where we are coming from a service result to another service field where the 
                    // service is the Query Context. Which we are assuming is likely an EF context and we don't need the null check
                    // Use ExecutionOptions.ExecuteServiceFieldsSeparately = false to disable this behaviour
                    var nullCheck = Field!.Services.Any(s => s.Type != Field.Schema.QueryContextType);
                    (resultExpression, PossibleNextContextTypes) = ExpressionUtil.MakeSelectWithDynamicType(this, nextFieldContext!, listContext, selectionFields, nullCheck);
                }
            }

            // have this return both the dynamic types so we can use them next, post-service
            if (resultExpression == null)
                (resultExpression, PossibleNextContextTypes) = ExpressionUtil.MakeSelectWithDynamicType(this, nextFieldContext!, listContext, selectionFields, false);

            // Make sure lists are evaluated and not deferred otherwise the second pass with services will fail if it needs to wrap for null check above
            // root level is handled in ExecutableGraphQLStatement with a null check
            if (AllowToList && !isRoot && resultExpression.Type.IsEnumerableOrArray() && !resultExpression.Type.IsDictionary())
                resultExpression = Expression.Call(typeof(EnumerableExtensions), nameof(EnumerableExtensions.ToListWithNullCheck), new[] { resultExpression.Type.GetEnumerableOrArrayType()! }, resultExpression, Expression.Constant(Field!.ReturnType.TypeNotNullable));

            return resultExpression;
        }

        protected override ParameterExpression? HandleBulkResolverForField(CompileContext compileContext, BaseGraphQLField field, IBulkFieldResolver bulkResolver, ParameterExpression? docParam, object? docVariables, ParameterReplacer replacer)
        {
            // Need the args that may be used in the bulk resolver expression
            var argumentValue = default(object);
            var validationErrors = new List<string>();
            var bulkFieldArgParam = bulkResolver.BulkArgParam;
            var newArgParam = bulkFieldArgParam != null ? Expression.Parameter(bulkFieldArgParam!.Type, $"{bulkFieldArgParam.Name}_exec") : null;
            compileContext.AddArgsToCompileContext(field.Field!, field.Arguments, docParam, docVariables, ref argumentValue, validationErrors, newArgParam);

            // replace the arg param after extensions (don't rely on extensions to do this)
            Expression bulkFieldExpr = bulkResolver.FieldExpression;

            GraphQLHelper.ValidateAndReplaceFieldArgs(field.Field!, bulkFieldArgParam, replacer, ref argumentValue, ref bulkFieldExpr, validationErrors, newArgParam);
            var listExpression = ListExpression;
            var parentNode = ParentNode;
            var rootParameter = RootParameter;
            var contextField = Field;
            while (parentNode != null)
            {
                Type typeDotnet = Field!.ReturnType.SchemaType.TypeDotnet;
                if (parentNode is GraphQLListSelectionField parentListNode)
                {
                    if (parentListNode.ToSingleNode != null)
                    {
                        listExpression = replacer.Replace(listExpression, rootParameter!, parentListNode.ToSingleNode.NextFieldContext!);
                        var nullCheck = Expression.MakeBinary(ExpressionType.Equal, parentListNode.ToSingleNode.NextFieldContext!, Expression.Constant(null, parentListNode.ToSingleNode.NextFieldContext!.Type));
                        listExpression = Expression.Condition(nullCheck, Expression.NewArrayInit(typeDotnet), listExpression, typeof(IEnumerable<>).MakeGenericType(typeDotnet));
                        rootParameter = parentNode.RootParameter;
                    }
                    else
                    {
                        // We can do SelectManyWithNullCheck in memory as services are post EF
                        listExpression = Expression.Call(typeof(EnumerableExtensions), nameof(EnumerableExtensions.SelectManyWithNullCheck), [rootParameter!.Type, typeDotnet], parentListNode.ListExpression!, Expression.Lambda(listExpression, rootParameter!));
                        rootParameter = parentNode.RootParameter;
                    }
                }
                else if (parentNode is GraphQLObjectProjectionField parentObjectNode)
                {
                    listExpression = replacer.Replace(listExpression, rootParameter!, parentObjectNode.NextFieldContext!);
                    var nullCheck = Expression.MakeBinary(ExpressionType.Equal, parentObjectNode.NextFieldContext!, Expression.Constant(null, parentObjectNode.NextFieldContext!.Type));
                    listExpression = Expression.Condition(nullCheck, Expression.NewArrayInit(typeDotnet), listExpression, typeof(IEnumerable<>).MakeGenericType(typeDotnet));
                    rootParameter = parentNode.RootParameter;
                }
                contextField = parentNode.Field;
                parentNode = parentNode.ParentNode;
            }
            compileContext.AddBulkResolver(bulkResolver.Name, bulkResolver.DataSelector, (LambdaExpression)bulkFieldExpr, listExpression, bulkResolver.ExtractedFields);
            compileContext.AddServices(field.Field!.Services);
            return newArgParam;
        }
    }
}

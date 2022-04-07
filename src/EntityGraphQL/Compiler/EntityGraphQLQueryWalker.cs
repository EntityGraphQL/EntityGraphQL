using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using System.Collections.Generic;
using EntityGraphQL.Compiler.Util;
using System;
using EntityGraphQL.Extensions;
using HotChocolate.Language;
using EntityGraphQL.Compiler.EntityQuery;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Visits nodes of a GraphQL request to build a representation of the query against the context objects via LINQ methods.
    /// </summary>
    /// <typeparam name="IGraphQLBaseNode"></typeparam>
    internal class EntityGraphQLQueryWalker : QuerySyntaxWalker<IGraphQLNode>
    {
        private readonly ISchemaProvider schemaProvider;
        private QueryRequestContext requestContext;

        /// <summary>
        /// The root - the query document. This is what we "return"
        /// </summary>
        /// <value></value>
        public GraphQLDocument Document { get; private set; }

        public EntityGraphQLQueryWalker(ISchemaProvider schemaProvider)
        {
            this.schemaProvider = schemaProvider;
        }

        /// <summary>
        /// This is out TOP level GQL document
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override void VisitDocument(DocumentNode node, IGraphQLNode context)
        {
            if (context != null)
                throw new ArgumentException("context should be null", nameof(context));

            context = Document = new GraphQLDocument(schemaProvider.SchemaFieldNamer);
            base.VisitDocument(node, context);
        }
        protected override void VisitOperationDefinition(OperationDefinitionNode node, IGraphQLNode context)
        {
            QueryWalkerHelper.ProcessVariableDefinitions(schemaProvider, requestContext.Query.Variables, node);
            var rootParameterContext = Expression.Parameter(schemaProvider.ContextType, $"ctx");

            if (node.Operation == OperationType.Query)
            {
                context = new GraphQLQueryStatement(node.Name?.Value, rootParameterContext, rootParameterContext, context);
            }
            else if (node.Operation == OperationType.Mutation)
            {
                context = new GraphQLMutationStatement(node.Name?.Value, rootParameterContext, rootParameterContext, context);
            }
            else if (node.Operation == OperationType.Subscription)
            {
                context = null; // we don't support subscription yet
            }

            if (context != null)
            {
                Document.Operations.Add((ExecutableGraphQLStatement)context);
                base.VisitOperationDefinition(node, context);
            }
        }

        public void Visit(DocumentNode document, QueryRequestContext context)
        {
            this.requestContext = context;
            this.Visit(document, (IGraphQLNode)null);
        }

        protected override void VisitField(FieldNode node, IGraphQLNode context)
        {
            var fieldName = node.Name.Value;
            string schemaTypeName = schemaProvider.GetSchemaTypeForDotnetType(context.NextFieldContext.Type).Name;
            var actualField = schemaProvider.GetActualField(schemaTypeName, fieldName, requestContext);

            var args = node.Arguments != null ? ProcessArguments(actualField, node.Arguments) : null;
            var alias = node.Alias?.Value;

            QueryWalkerHelper.CheckRequiredArguments(actualField, args);

            if (schemaProvider.HasMutation(actualField.Name))
            {
                var resultName = alias ?? actualField.Name;
                var mutationType = schemaProvider.GetMutations().First(m => m.Name == actualField.Name);

                var nextContextParam = Expression.Parameter(mutationType.ReturnType.TypeDotnet, $"mut_{actualField.Name}");
                var mutationField = new GraphQLMutationField(resultName, mutationType, args, nextContextParam, nextContextParam, context);

                if (node.SelectionSet != null)
                {
                    BaseGraphQLQueryField select = ParseFieldSelect(nextContextParam, actualField, resultName, mutationField, node.SelectionSet, args);
                    if (mutationType.ReturnType.IsList)
                    {
                        // nulls are not known until mutation is executed. Will be handled in GraphQLMutationStatement
                        var newSelect = new GraphQLListSelectionField(actualField, actualField.Extensions, resultName, (ParameterExpression)select.NextFieldContext, select.RootParameter, select.RootParameter, context, args);
                        foreach (var queryField in select.QueryFields)
                        {
                            newSelect.AddField(queryField);
                        }
                        select = newSelect;
                    }
                    mutationField.ResultSelection = select;
                }
                context.AddField(mutationField);
            }
            else
            {
                BaseGraphQLField fieldResult = null;
                var resultName = alias ?? actualField.Name;

                var nodeExpression = actualField.Resolve;

                if (node.SelectionSet != null)
                {
                    fieldResult = ParseFieldSelect(nodeExpression, actualField, resultName, context, node.SelectionSet, args);
                }
                else
                {
                    fieldResult = new GraphQLScalarField((Field)actualField, actualField.Extensions, resultName, nodeExpression, context.NextFieldContext as ParameterExpression ?? context.RootParameter, context, args);
                }

                if (node.Directives?.Any() == true)
                {
                    fieldResult = ProcessFieldDirectives(fieldResult, node.Directives);
                }
                if (fieldResult != null)
                {
                    context.AddField(fieldResult);
                }
            }
        }

        public BaseGraphQLQueryField ParseFieldSelect(Expression fieldExp, IField fieldContext, string name, IGraphQLNode context, SelectionSetNode selection, Dictionary<string, Expression> arguments)
        {
            if (fieldContext.ReturnType.IsList)
            {
                return BuildDynamicSelectOnCollection(fieldContext, fieldExp, fieldContext.ReturnType.SchemaType, name, context, selection, arguments);
            }

            var graphQLNode = BuildDynamicSelectForObjectGraph(fieldContext, fieldExp, context, name, selection, arguments);
            // Could be a list.First().Blah that we need to turn into a select, or
            // other levels are object selection. e.g. from the top level people query I am selecting all their children { field1, etc. }
            // Can we turn a list.First().Blah into and list.Select(i => new {i.Blah}).First()
            var listExp = ExpressionUtil.FindEnumerable(fieldExp);
            if (listExp.Item1 != null)
            {
                // yes we can
                // rebuild the Expression so we keep any ConstantParameters
                var item1 = listExp.Item1;
                var returnType = schemaProvider.GetSchemaTypeForDotnetType(item1.Type.GetEnumerableOrArrayType());
                // TODO this doubles the field visit
                var collectionNode = BuildDynamicSelectOnCollection(fieldContext, item1, returnType, name, context, selection, arguments);
                return new GraphQLCollectionToSingleField(collectionNode, graphQLNode, listExp.Item2);
            }
            return graphQLNode;
        }

        /// <summary>
        /// Given a syntax of someCollection { fields, to, selection, from, object }
        /// it will build a select assuming 'someCollection' is an IEnumerable
        /// </summary>
        private GraphQLListSelectionField BuildDynamicSelectOnCollection(IField actualField, Expression nodeExpression, ISchemaType returnType, string resultName, IGraphQLNode context, SelectionSetNode selection, Dictionary<string, Expression> arguments)
        {
            var elementType = returnType.TypeDotnet;
            var fieldParam = Expression.Parameter(elementType, $"p_{elementType.Name}");

            var gqlNode = new GraphQLListSelectionField(actualField, actualField.Extensions, resultName, fieldParam, context.RootParameter, nodeExpression, context, arguments);

            // visit child fields. Will be more fields
            base.VisitSelectionSet(selection, gqlNode);
            return gqlNode;
        }

        /// <summary>
        /// Given a syntax of { fields, to, selection, from, object } with a context
        /// it will build the correct select statement
        /// </summary>
        /// <param name="name"></param>
        /// <param name="context"></param>
        /// <param name="selectContext"></param>
        /// <returns></returns>
        private GraphQLObjectProjectionField BuildDynamicSelectForObjectGraph(IField actualField, Expression nodeExpression, IGraphQLNode context, string name, SelectionSetNode selection, Dictionary<string, Expression> arguments)
        {
            var graphQLNode = new GraphQLObjectProjectionField(actualField, actualField.Extensions, name, nodeExpression, context.NextFieldContext as ParameterExpression ?? context.RootParameter, context, arguments);

            base.VisitSelectionSet(selection, graphQLNode);

            return graphQLNode;
        }

        public Dictionary<string, Expression> ProcessArguments(IField field, IEnumerable<ArgumentNode> queryArguments)
        {
            var args = queryArguments.ToDictionary(a => a.Name.Value, a =>
            {
                var argName = a.Name.Value;
                if (!field.Arguments.ContainsKey(argName))
                {
                    throw new EntityGraphQLCompilerException($"No argument '{argName}' found on field '{field.Name}'");
                }
                var r = ParseArgument(field, a);
                return r;
            });
            return args;
        }

        public Expression ParseArgument(IField fieldArgumentContext, ArgumentNode argument)
        {
            string argName = argument.Name.Value;
            var argType = fieldArgumentContext.GetArgumentType(argName);
            var constVal = QueryWalkerHelper.ProcessArgumentOrVariable(schemaProvider, requestContext.Query.Variables, argument, argType.Type.TypeDotnet);
            Expression argValue = Expression.Constant(constVal);

            if (argValue != null && argValue.Type == typeof(string) && argValue.NodeType == ExpressionType.Constant)
            {
                if (argType.Type.TypeDotnet.IsConstructedGenericType && argType.Type.TypeDotnet.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
                {
                    string query = (string)constVal;
                    return BuildEntityQueryExpression(fieldArgumentContext, fieldArgumentContext.Name, argName, query);
                }
            }
            return argValue;
        }

        private BaseGraphQLField ProcessFieldDirectives(BaseGraphQLField fieldResult, IEnumerable<DirectiveNode> directives)
        {
            foreach (var directive in directives)
            {
                var processor = schemaProvider.GetDirective(directive.Name.Value);
                var argType = processor.GetArgumentsType();
                var argObj = Activator.CreateInstance(argType);
                foreach (var arg in directive.Arguments)
                {
                    var prop = argType.GetProperty(arg.Name.Value);
                    var argVal = QueryWalkerHelper.ProcessArgumentOrVariable(schemaProvider, requestContext.Query.Variables, arg, prop.PropertyType);
                    prop.SetValue(argObj, argVal);
                }
                fieldResult = processor.ProcessField(fieldResult, argObj);

                if (fieldResult == null)
                    break;
            }
            return fieldResult;
        }

        private Expression BuildEntityQueryExpression(IField fieldArgumentContext, string fieldName, string argName, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return null;
            }
            var prop = ((Field)fieldArgumentContext).Arguments.Values.FirstOrDefault(p => p.Name == argName && p.Type.TypeDotnet.GetGenericTypeDefinition() == typeof(EntityQueryType<>));
            if (prop == null)
                throw new EntityGraphQLCompilerException($"Can not find argument {argName} of type EntityQuery on field {fieldName}");

            var eqlt = (BaseEntityQueryType)prop.DefaultValue;
            var contextParam = Expression.Parameter(eqlt.QueryType, $"q_{eqlt.QueryType.Name}");
            Expression expression = EntityQueryCompiler.CompileWith(query, contextParam, schemaProvider, requestContext).ExpressionResult.Expression;
            expression = Expression.Lambda(expression, contextParam);
            return expression;
        }

        protected override void VisitFragmentDefinition(FragmentDefinitionNode node, IGraphQLNode context)
        {
            // top level statement in GQL doc. Defines the fragment fields.
            // Add to the fragments and return null
            var typeName = node.TypeCondition.Name.Value;

            var fragParameter = Expression.Parameter(schemaProvider.Type(typeName).TypeDotnet, $"frag_{typeName}");
            var fragDef = new GraphQLFragmentStatement(node.Name.Value, fragParameter, fragParameter);

            ((GraphQLDocument)context).Fragments.Add(fragDef);

            base.VisitFragmentDefinition(node, fragDef);
        }

        protected override void VisitFragmentSpread(FragmentSpreadNode node, IGraphQLNode context)
        {
            // later when executing we turn this field into the defined fragment (as the fragment may be defined after use)
            // Just store the name to look up when needed
            var name = node.Name.Value;
            BaseGraphQLField fragField = new GraphQLFragmentField(name, null, context.RootParameter, context);
            if (node.Directives?.Any() == true)
            {
                fragField = ProcessFieldDirectives(fragField, node.Directives);
            }
            if (fragField != null)
            {
                base.VisitFragmentSpread(node, fragField);
                context.AddField(fragField);
            }
        }
    }
}

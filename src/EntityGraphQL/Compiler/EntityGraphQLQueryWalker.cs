using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using System.Collections.Generic;
using EntityGraphQL.Compiler.Util;
using System;
using EntityGraphQL.Extensions;
using HotChocolate.Language;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Visits nodes of a GraphQL request to build a representation of the query against the context objects via LINQ methods.
    /// </summary>
    /// <typeparam name="IGraphQLBaseNode"></typeparam>
    internal class EntityGraphQLQueryWalker : QuerySyntaxWalker<IGraphQLNode?>
    {
        private readonly ISchemaProvider schemaProvider;
        private readonly QueryVariables variables;
        private readonly QueryRequestContext requestContext;
        private ExecutableGraphQLStatement? currentOperation;

        /// <summary>
        /// The root - the query document. This is what we "return"
        /// </summary>
        /// <value></value>
        public GraphQLDocument? Document { get; private set; }

        public EntityGraphQLQueryWalker(ISchemaProvider schemaProvider, QueryVariables? variables, QueryRequestContext context)
        {
            this.requestContext = context;
            this.schemaProvider = schemaProvider;
            if (variables == null)
                variables = new QueryVariables();
            this.variables = variables;
        }

        /// <summary>
        /// This is out TOP level GQL document
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override void VisitDocument(DocumentNode node, IGraphQLNode? context)
        {
            if (context != null)
                throw new ArgumentException("context should be null", nameof(context));

            Document = new GraphQLDocument(schemaProvider.SchemaFieldNamer);
            base.VisitDocument(node, context);
        }
        protected override void VisitOperationDefinition(OperationDefinitionNode node, IGraphQLNode? context)
        {
            if (Document == null)
                throw new EntityGraphQLCompilerException("Document should not be null visiting operation definition");

            // these are the variables that can change each request for the same query
            var operationVariables = ProcessVariableDefinitions(node);

            if (node.Operation == OperationType.Query)
            {
                var rootParameterContext = Expression.Parameter(schemaProvider.QueryContextType, $"op_ctx");
                context = new GraphQLQueryStatement(schemaProvider, node.Name?.Value ?? string.Empty, rootParameterContext, rootParameterContext, operationVariables);
                currentOperation = (GraphQLQueryStatement)context;
            }
            else if (node.Operation == OperationType.Mutation)
            {
                // we never build expression from this parameter but the type is used to look up the ISchemaType
                var rootParameterContext = Expression.Parameter(schemaProvider.MutationType, $"mut_ctx");
                context = new GraphQLMutationStatement(schemaProvider, node.Name?.Value ?? string.Empty, rootParameterContext, rootParameterContext, operationVariables);
                currentOperation = (GraphQLMutationStatement)context;
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

        private Dictionary<string, ArgType> ProcessVariableDefinitions(OperationDefinitionNode node)
        {
            if (Document == null)
                throw new EntityGraphQLCompilerException("Document should not be null visiting operation definition");

            var documentVariables = new Dictionary<string, ArgType>();

            foreach (var item in node.VariableDefinitions)
            {
                var argName = item.Variable.Name.Value;
                object? defaultValue = null;
                (var gqlTypeName, var isList, var isRequired) = GetGqlType(item);

                var schemaType = schemaProvider.GetSchemaType(gqlTypeName, null);
                var varType = schemaType.TypeDotnet;
                if (varType == null)
                    throw new EntityGraphQLCompilerException($"Variable {argName} has no type");
                if (isList)
                    varType = typeof(List<>).MakeGenericType(varType);

                if (item.DefaultValue != null)
                    defaultValue = Expression.Lambda(Expression.Constant(QueryWalkerHelper.ProcessArgumentValue(schemaProvider, item.DefaultValue, argName, varType))).Compile().DynamicInvoke();

                documentVariables.Add(argName, new ArgType(gqlTypeName, schemaType.TypeDotnet.Name, new GqlTypeInfo(() => schemaType, varType)
                {
                    TypeNotNullable = isRequired,
                    ElementTypeNullable = !isRequired
                }, null, varType)
                {
                    DefaultValue = defaultValue,
                    IsRequired = isRequired
                });

                if (item.Type.Kind == SyntaxKind.NonNullType && variables.ContainsKey(argName) == false)
                {
                    throw new EntityGraphQLCompilerException($"Missing required variable '{argName}' on operation '{node.Name?.Value}'");
                }
            }
            return documentVariables;
        }

        private static (string typeName, bool isList, bool isRequired) GetGqlType(ISyntaxNode item)
        {
            return item.Kind switch
            {
                SyntaxKind.NamedType => (((NamedTypeNode)item).Name.Value, false, false),
                SyntaxKind.NonNullType => (((NonNullTypeNode)item).NamedType().Name.Value, false, true),
                SyntaxKind.VariableDefinition => (((VariableDefinitionNode)item).Type.NamedType().Name.Value, ((VariableDefinitionNode)item).Type.Kind == SyntaxKind.ListType, ((VariableDefinitionNode)item).Type.NamedType().Kind == SyntaxKind.NonNullType),
                SyntaxKind.ListType => (((ListTypeNode)item).Type.NamedType().Name.Value, true, ((VariableDefinitionNode)item).Type.NamedType().Kind == SyntaxKind.NonNullType),
                _ => throw new EntityGraphQLCompilerException($"Unexpected node kind {item.Kind}"),
            };
        }

        protected override void VisitField(FieldNode node, IGraphQLNode? context)
        {
            if (context == null)
                throw new EntityGraphQLCompilerException("context should not be null visiting field");
            if (context.NextFieldContext == null)
                throw new EntityGraphQLCompilerException("context.NextFieldContext should not be null visiting field");

            var schemaType = schemaProvider.GetSchemaType(context.NextFieldContext.Type, requestContext);
            var actualField = schemaType.GetField(node.Name.Value, requestContext);

            // so the op knows what services it needs to inject
            currentOperation?.AddServices(actualField.Services);

            var args = node.Arguments != null ? ProcessArguments(actualField, node.Arguments) : null;
            var resultName = node.Alias?.Value ?? actualField.Name;

            if (actualField.FieldType == Schema.GraphQLQueryFieldType.Mutation)
            {
                var mutationField = (MutationField)actualField;

                var nextContextParam = Expression.Parameter(mutationField.ReturnType.TypeDotnet, $"mut_{actualField.Name}");
                var graphqlMutationField = new GraphQLMutationField(schemaProvider, resultName, mutationField, args, nextContextParam, nextContextParam, context);

                if (node.SelectionSet != null)
                {
                    BaseGraphQLQueryField select = ParseFieldSelect(nextContextParam, actualField, resultName, graphqlMutationField, node.SelectionSet, args);
                    if (mutationField.ReturnType.IsList)
                    {
                        // nulls are not known until mutation is executed. Will be handled in GraphQLMutationStatement
                        var newSelect = new GraphQLListSelectionField(schemaProvider, actualField, resultName, (ParameterExpression)select.NextFieldContext!, select.RootParameter, select.RootParameter!, context, args);
                        foreach (var queryField in select.QueryFields)
                        {
                            newSelect.AddField(queryField);
                        }
                        select = newSelect;
                    }
                    graphqlMutationField.ResultSelection = select;
                }
                if (node.Directives?.Any() == true)
                {
                    graphqlMutationField.AddDirectives(ProcessFieldDirectives(node.Directives));
                }

                context.AddField(graphqlMutationField);
            }
            else
            {
                BaseGraphQLField? fieldResult;

                if (node.SelectionSet != null)
                {
                    fieldResult = ParseFieldSelect(actualField.ResolveExpression!, actualField, resultName, context, node.SelectionSet, args);
                }
                else
                {
                    fieldResult = new GraphQLScalarField(schemaProvider, actualField, resultName, actualField.ResolveExpression!, context.NextFieldContext as ParameterExpression ?? context.RootParameter, context, args);
                }

                if (node.Directives?.Any() == true)
                {
                    fieldResult.AddDirectives(ProcessFieldDirectives(node.Directives));
                }
                if (fieldResult != null)
                {
                    context.AddField(fieldResult);
                }
            }
        }

        public BaseGraphQLQueryField ParseFieldSelect(Expression fieldExp, IField fieldContext, string name, IGraphQLNode context, SelectionSetNode selection, Dictionary<string, object>? arguments)
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
                var returnType = schemaProvider.GetSchemaType(listExp.Item1.Type.GetEnumerableOrArrayType()!, requestContext);
                // TODO this doubles the field visit
                var collectionNode = BuildDynamicSelectOnCollection(fieldContext, listExp.Item1, returnType, name, context, selection, arguments);
                return new GraphQLCollectionToSingleField(schemaProvider, collectionNode, graphQLNode, listExp.Item2!);
            }
            return graphQLNode;
        }

        /// <summary>
        /// Given a syntax of someCollection { fields, to, selection, from, object }
        /// it will build a select assuming 'someCollection' is an IEnumerable
        /// </summary>
        private GraphQLListSelectionField BuildDynamicSelectOnCollection(IField actualField, Expression nodeExpression, ISchemaType returnType, string resultName, IGraphQLNode context, SelectionSetNode selection, Dictionary<string, object>? arguments)
        {
            if (context == null)
                throw new EntityGraphQLCompilerException("context should not be null building select on collection");

            var elementType = returnType.TypeDotnet;
            var fieldParam = Expression.Parameter(elementType, $"p_{elementType.Name}");

            var gqlNode = new GraphQLListSelectionField(schemaProvider, actualField, resultName, fieldParam, context.RootParameter, nodeExpression, context, arguments);

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
        private GraphQLObjectProjectionField BuildDynamicSelectForObjectGraph(IField actualField, Expression nodeExpression, IGraphQLNode context, string name, SelectionSetNode selection, Dictionary<string, object>? arguments)
        {
            if (context == null)
                throw new EntityGraphQLCompilerException("context should not be null visiting field");
            if (context.NextFieldContext == null && context.RootParameter == null)
                throw new EntityGraphQLCompilerException("context.NextFieldContext and context.RootParameter should not be null visiting field");
            var graphQLNode = new GraphQLObjectProjectionField(schemaProvider, actualField, name, nodeExpression, context.NextFieldContext as ParameterExpression ?? context.RootParameter!, context, arguments);

            base.VisitSelectionSet(selection, graphQLNode);

            return graphQLNode;
        }

        private Dictionary<string, object> ProcessArguments(IField field, IEnumerable<ArgumentNode> queryArguments)
        {
            var args = new Dictionary<string, object>();
            foreach (var arg in queryArguments)
            {
                var argName = arg.Name.Value;
                if (!field.Arguments.ContainsKey(argName))
                {
                    throw new EntityGraphQLCompilerException($"No argument '{argName}' found on field '{field.Name}'");
                }
                var r = ParseArgument(argName, field, arg);
                if (r != null)
                    args.Add(argName, r);
            }
            return args;
        }

        private object? ParseArgument(string argName, IField fieldArgumentContext, ArgumentNode argument)
        {
            if (Document == null)
                throw new EntityGraphQLCompilerException("Document should not be null when visiting arguments");

            return ProcessArgumentOrVariable(argName, schemaProvider, argument, fieldArgumentContext.GetArgumentType(argName).Type.TypeDotnet);
        }

        /// <summary>
        /// Build the expression for the argument. A Variable ($name) will be a Expression.Parameter
        /// A inline value will be a Expression.Constant
        /// </summary>
        private object? ProcessArgumentOrVariable(string argName, ISchemaProvider schema, ArgumentNode argument, Type argType)
        {
            if (currentOperation == null)
                throw new EntityGraphQLCompilerException("currentOperation should not be null when visiting arguments");

            if (argument.Value.Kind == SyntaxKind.Variable)
            {
                return Expression.PropertyOrField(currentOperation.OpVariableParameter, ((VariableNode)argument.Value).Name.Value);
            }
            return QueryWalkerHelper.ProcessArgumentValue(schema, argument.Value, argName, argType);
        }

        private List<GraphQLDirective> ProcessFieldDirectives(IEnumerable<DirectiveNode> directives)
        {
            var result = new List<GraphQLDirective>();
            foreach (var directive in directives)
            {
                var processor = schemaProvider.GetDirective(directive.Name.Value);
                var argType = processor.GetArgumentsType();
                var args = new Dictionary<string, object>();
                foreach (var arg in directive.Arguments)
                {
                    var argVal = ProcessArgumentOrVariable(arg.Name.Value, schemaProvider, arg, argType.GetProperty(arg.Name.Value).PropertyType);
                    if (argVal != null)
                        args.Add(arg.Name.Value, argVal);
                }
                result.Add(new GraphQLDirective(directive.Name.Value, processor, args));
            }
            return result;
        }

        protected override void VisitFragmentDefinition(FragmentDefinitionNode node, IGraphQLNode? context)
        {
            if (Document == null)
                throw new EntityGraphQLCompilerException("Document can not be null in VisitFragmentDefinition");
            // top level statement in GQL doc. Defines the fragment fields.
            // Add to the fragments and return null
            var typeName = node.TypeCondition.Name.Value;

            var fragParameter = Expression.Parameter(schemaProvider.Type(typeName).TypeDotnet, $"frag_{typeName}");
            var fragDef = new GraphQLFragmentStatement(node.Name.Value, fragParameter, fragParameter);

            Document.Fragments.Add(fragDef);

            base.VisitFragmentDefinition(node, fragDef);
        }

        protected override void VisitFragmentSpread(FragmentSpreadNode node, IGraphQLNode? context)
        {
            if (context == null)
                throw new EntityGraphQLCompilerException("Context is null in FragmentSpread");
            if (context.RootParameter == null)
                throw new EntityGraphQLCompilerException("Fragment spread can only be used inside a selection set (context.RootParameter is null)");
            // later when executing we turn this field into the defined fragment (as the fragment may be defined after use)
            // Just store the name to look up when needed
            BaseGraphQLField? fragField = new GraphQLFragmentField(schemaProvider, node.Name.Value, null, context.RootParameter, context, Document!);
            if (node.Directives?.Any() == true)
            {
                fragField.AddDirectives(ProcessFieldDirectives(node.Directives));
            }
            if (fragField != null)
            {
                base.VisitFragmentSpread(node, fragField);
                context.AddField(fragField);
            }
        }
    }
}

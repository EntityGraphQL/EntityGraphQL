using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Grammer;
using EntityGraphQL.Schema;
using System.Collections.Generic;
using EntityGraphQL.LinqQuery;
using EntityGraphQL.Compiler.Util;
using System.Security.Claims;
using System;
using System.Reflection;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Visits nodes of a GraphQL request to build a representation of the query against the context objects via LINQ methods.
    /// </summary>
    /// <typeparam name="IGraphQLBaseNode"></typeparam>
    internal class GraphQLVisitor : EntityGraphQLBaseVisitor<IGraphQLBaseNode>
    {
        private readonly ConstantVisitor constantVisitor;
        private readonly ClaimsIdentity claims;
        private readonly ISchemaProvider schemaProvider;
        private readonly IMethodProvider methodProvider;
        private readonly QueryVariables variables;

        // This is really just so we know what to use when visiting a field
        private ExpressionResult currentExpressionContext;
        /// <summary>
        /// As we parse the request fragments are added to this
        /// </summary>
        private readonly List<GraphQLFragment> fragments = new List<GraphQLFragment>();

        public GraphQLVisitor(ISchemaProvider schemaProvider, IMethodProvider methodProvider, QueryVariables variables, ClaimsIdentity claims)
        {
            this.claims = claims;
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
            this.variables = variables;
            this.constantVisitor = new ConstantVisitor(schemaProvider);
        }

        public override IGraphQLBaseNode VisitField(EntityGraphQLParser.FieldContext context)
        {
            var fieldName = context.fieldDef.GetText();
            string schemaTypeName = schemaProvider.GetSchemaTypeNameForDotnetType(currentExpressionContext.Type);
            var actualFieldName = schemaProvider.GetActualFieldName(schemaTypeName, fieldName, claims);

            var args = context.argsCall != null ? ParseGqlCall(actualFieldName, context.argsCall) : null;
            var alias = context.alias?.name.GetText();

            if (schemaProvider.HasMutation(actualFieldName))
            {
                var mutationType = schemaProvider.GetMutations().First(m => m.Name == actualFieldName);
                if (context.select != null)
                {
                    var expContext = (ExpressionResult)Expression.Parameter(mutationType.ReturnType.SchemaType.ContextType, $"mut_{actualFieldName}");
                    var oldContext = currentExpressionContext;
                    currentExpressionContext = expContext;
                    var select = ParseFieldSelect(expContext, actualFieldName, context.select);
                    currentExpressionContext = oldContext;
                    return new GraphQLMutationNode(mutationType, args, (GraphQLQueryNode)select, schemaProvider.SchemaFieldNamer);
                }
                else
                {
                    var resultName = alias ?? actualFieldName;

                    return new GraphQLMutationNode(mutationType, args, null, schemaProvider.SchemaFieldNamer);
                }
            }
            else
            {
                if (!schemaProvider.TypeHasField(schemaTypeName, actualFieldName, args != null ? args.Select(d => d.Key) : new string[0], claims))
                    throw new EntityGraphQLCompilerException($"Field {actualFieldName} not found on type {schemaTypeName}");

                var result = schemaProvider.GetExpressionForField(currentExpressionContext, schemaTypeName, actualFieldName, args, claims);

                IGraphQLBaseNode fieldResult;
                var resultName = alias ?? actualFieldName;

                if (context.select != null)
                {
                    fieldResult = ParseFieldSelect(result, resultName, context.select);
                }
                else
                {
                    fieldResult = new GraphQLQueryNode(schemaProvider, fragments, resultName, result, currentExpressionContext.AsParameter(), null, null);
                }

                if (context.directive != null)
                {
                    return ProcessFieldDirective((GraphQLQueryNode)fieldResult, context.directive);
                }
                return fieldResult;
            }
        }

        private IGraphQLBaseNode ProcessFieldDirective(GraphQLQueryNode fieldResult, EntityGraphQLParser.DirectiveCallContext directive)
        {
            var processor = schemaProvider.GetDirective(directive.name.GetText());
            var argList = directive.directiveArgs.children.Where(c => c.GetType() == typeof(EntityGraphQLParser.GqlargContext)).Cast<EntityGraphQLParser.GqlargContext>();
            var args = argList.ToDictionary(a => a.gqlfield.GetText(), a => ParseGqlarg(null, a));
            var argType = processor.GetArgumentsType();
            var argObj = Activator.CreateInstance(argType);
            foreach (var arg in args)
            {
                var prop = argType.GetProperty(arg.Key);
                prop.SetValue(argObj, Expression.Lambda(arg.Value.Expression).Compile().DynamicInvoke());
            }
            var result = processor.ProcessQueryInternal(fieldResult, argObj);
            return result;
        }

        /// <summary>
        /// Build a Select() on the context of the field type
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IGraphQLBaseNode ParseFieldSelect(ExpressionResult expContext, string name, EntityGraphQLParser.ObjectSelectionContext context)
        {
            try
            {
                IGraphQLBaseNode graphQLNode = null;
                if (expContext.Type.IsEnumerableOrArray())
                {
                    graphQLNode = BuildDynamicSelectOnCollection(expContext, name, context);
                }
                else
                {
                    // Could be a list.First() that we need to turn into a select, or
                    // other levels are object selection. e.g. from the top level people query I am selecting all their children { field1, etc. }
                    // Can we turn a list.First() into and list.Select().First()
                    var listExp = ExpressionUtil.FindIEnumerable(expContext);
                    if (listExp.Item1 != null)
                    {
                        // yes we can
                        // rebuild the ExpressionResult so we keep any ConstantParameters
                        var item1 = (ExpressionResult)listExp.Item1;
                        item1.AddConstantParameters(expContext.ConstantParameters);
                        item1.AddServices(expContext.Services);
                        graphQLNode = BuildDynamicSelectOnCollection(item1, name, context);
                        graphQLNode.SetCombineExpression(listExp.Item2);
                    }
                    else
                    {
                        graphQLNode = BuildDynamicSelectForObjectGraph(expContext, currentExpressionContext.AsParameter(), name, context);
                    }
                }
                return graphQLNode;
            }
            catch (EntityGraphQLCompilerException ex)
            {
                throw SchemaException.MakeFieldCompileError($"Error compiling field {name}", ex.Message);
            }
        }

        public Dictionary<string, ExpressionResult> ParseGqlCall(string fieldName, EntityGraphQLParser.GqlCallContext context)
        {
            var argList = context.gqlarguments.children.Where(c => c.GetType() == typeof(EntityGraphQLParser.GqlargContext)).Cast<EntityGraphQLParser.GqlargContext>();
            IField methodType = schemaProvider.GetFieldOnContext(currentExpressionContext, fieldName, claims);
            var args = argList.ToDictionary(a => a.gqlfield.GetText(), a =>
            {
                var argName = a.gqlfield.GetText();
                if (!methodType.Arguments.ContainsKey(argName))
                {
                    throw new EntityGraphQLCompilerException($"No argument '{argName}' found on field '{methodType.Name}'");
                }
                var r = ParseGqlarg(methodType, a);
                return r;
            });
            return args;
        }

        public ExpressionResult ParseGqlarg(IField fieldArgumentContext, EntityGraphQLParser.GqlargContext context)
        {
            ExpressionResult gqlVarValue = null;
            if (context.gqlVar() != null)
            {
                string varKey = context.gqlVar().GetText().TrimStart('$');
                if (variables == null)
                {
                    throw new EntityGraphQLCompilerException($"Missing variable {varKey}");
                }
                object value = variables.GetValueFor(varKey);
                gqlVarValue = (ExpressionResult)Expression.Constant(value);
            }
            else
            {
                // this is an expression
                gqlVarValue = constantVisitor.Visit(context.gqlvalue);
            }

            string argName = context.gqlfield.GetText();
            if (fieldArgumentContext != null && fieldArgumentContext.HasArgumentByName(argName))
            {
                var argType = fieldArgumentContext.GetArgumentType(argName);

                if (gqlVarValue != null && gqlVarValue.Type == typeof(string) && gqlVarValue.NodeType == ExpressionType.Constant)
                {
                    string strValue = (string)((ConstantExpression)gqlVarValue).Value;
                    if (
                        (argType.Type.TypeDotnet == typeof(Guid) || argType.Type.TypeDotnet == typeof(Guid?) ||
                        argType.Type.TypeDotnet == typeof(RequiredField<Guid>) || argType.Type.TypeDotnet == typeof(RequiredField<Guid?>)) && ConstantVisitor.GuidRegex.IsMatch(strValue))
                    {
                        return (ExpressionResult)Expression.Constant(Guid.Parse(strValue));
                    }
                    if (argType.Type.TypeDotnet.IsConstructedGenericType && argType.Type.TypeDotnet.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
                    {
                        string query = strValue;
                        if (query.StartsWith("\""))
                        {
                            query = query.Substring(1, context.gqlvalue.GetText().Length - 2);
                        }
                        return BuildEntityQueryExpression(fieldArgumentContext, fieldArgumentContext.Name, argName, query);
                    }

                    var argumentNonNullType = argType.Type.TypeDotnet.IsNullableType() ? Nullable.GetUnderlyingType(argType.Type.TypeDotnet) : argType.Type.TypeDotnet;
                    if (argumentNonNullType.GetTypeInfo().IsEnum)
                    {
                        var enumName = strValue;
                        var valueIndex = Enum.GetNames(argumentNonNullType).ToList().FindIndex(n => n == enumName);
                        if (valueIndex == -1)
                        {
                            throw new EntityGraphQLCompilerException($"Value {enumName} is not valid for argument {context.gqlfield.GetText()}");
                        }
                        var enumValue = Enum.GetValues(argumentNonNullType).GetValue(valueIndex);
                        return (ExpressionResult)Expression.Constant(enumValue);
                    }
                }
            }
            return gqlVarValue;
        }

        private ExpressionResult BuildEntityQueryExpression(IField fieldArgumentContext, string fieldName, string argName, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return null;
            }
            var prop = ((Field)fieldArgumentContext).ArgumentTypesObject.GetType().GetProperties().FirstOrDefault(p => p.Name == argName && p.PropertyType.GetGenericTypeDefinition() == typeof(EntityQueryType<>));
            if (prop == null)
                throw new EntityGraphQLCompilerException($"Can not find argument {argName} of type EntityQuery on field {fieldName}");

            var eqlt = prop.GetValue(((Field)fieldArgumentContext).ArgumentTypesObject) as BaseEntityQueryType;
            var contextParam = Expression.Parameter(eqlt.QueryType, $"q_{eqlt.QueryType.Name}");
            ExpressionResult expressionResult = EntityQueryCompiler.CompileWith(query, contextParam, schemaProvider, claims, methodProvider, variables).ExpressionResult;
            expressionResult = (ExpressionResult)Expression.Lambda(expressionResult.Expression, contextParam);
            return expressionResult;
        }

        /// Given a syntax of someCollection { fields, to, selection, from, object }
        /// it will build a select assuming 'someCollection' is an IEnumerables
        private IGraphQLBaseNode BuildDynamicSelectOnCollection(ExpressionResult queryResult, string resultName, EntityGraphQLParser.ObjectSelectionContext context)
        {
            var elementType = queryResult.Type.GetEnumerableOrArrayType();
            var contextParameter = Expression.Parameter(elementType, $"p_{elementType.Name}");

            var exp = queryResult;

            var oldContext = currentExpressionContext;
            currentExpressionContext = (ExpressionResult)contextParameter;
            // visit child fields. Will be more fields
            var fieldExpressions = context.children.Select(c => Visit(c)).Where(n => n != null).ToList();

            var gqlNode = new GraphQLQueryNode(schemaProvider, fragments, resultName, exp, oldContext.AsParameter(), fieldExpressions, (ExpressionResult)contextParameter);

            currentExpressionContext = oldContext;

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
        private IGraphQLBaseNode BuildDynamicSelectForObjectGraph(ExpressionResult selectFromExp, ParameterExpression selectFromParam, string name, EntityGraphQLParser.ObjectSelectionContext context)
        {
            try
            {
                // visit child fields. Will be field or entityQueries again
                // These expression will be built on the element type
                var oldContext = currentExpressionContext;
                currentExpressionContext = selectFromExp;
                ParameterExpression replacementParameter = null;
                // we might be using a service i.e. ctx => WithService((T r) => r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()))
                // if we can we want to avoid calling that multiple times with a expression like
                // r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()) == null ? null : new {
                //      Field = r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()).Blah
                // }
                // by wrapping the whole thing in a method that does the null check once.
                // This means we build the fieldExpressions on a parameter of the result type
                bool wrapField = currentExpressionContext.Services.Any();
                if (wrapField)
                {
                    // replace with a parameter. The expression is compiled at execution time once
                    replacementParameter = Expression.Parameter(selectFromExp.Type, "null_wrap");
                    currentExpressionContext = (ExpressionResult)replacementParameter;
                }
                var fieldExpressions = context.children.Select(c => Visit(c)).Where(n => n != null).ToList();
                currentExpressionContext = oldContext;

                var graphQLNode = new GraphQLQueryNode(schemaProvider, fragments, name, selectFromExp, selectFromParam, fieldExpressions, selectFromExp)
                {
                    IsWrapped = wrapField
                };
                return graphQLNode;
            }
            catch (EntityGraphQLCompilerException ex)
            {
                throw SchemaException.MakeFieldCompileError($"Failed compiling field {name}", ex.Message);
            }
        }

        /// <summary>
        /// This is out TOP level GQL result
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override IGraphQLBaseNode VisitGraphQL(EntityGraphQLParser.GraphQLContext context)
        {
            var gqlResult = new GraphQLResultNode();
            foreach (var c in context.children)
            {
                var node = (GraphQLQueryNode)Visit(c);
                if (node != null)
                    gqlResult.Operations.Add(node);
            }
            return gqlResult;
        }

        /// <summary>
        /// This is one of our top level node.
        /// query MyQuery {
        ///   entityQuery { fields [, field] },
        ///   entityQuery { fields [, field] },
        ///   ...
        /// }
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override IGraphQLBaseNode VisitDataQuery(EntityGraphQLParser.DataQueryContext context)
        {
            var operation = GetOperation(context.operationName());
            foreach (var item in operation.Arguments.Where(a => a.DefaultValue != null))
            {
                variables[item.ArgName] = Expression.Lambda(item.DefaultValue.Expression).Compile().DynamicInvoke();
            }
            this.currentExpressionContext = (ExpressionResult)Expression.Parameter(schemaProvider.ContextType, $"ctx");
            var rootFields = new List<GraphQLQueryNode>();
            // Just visit each child node. All top level will be entityQueries
            foreach (var c in context.objectSelection().children)
            {
                var n = Visit(c);
                if (n != null)
                    rootFields.Add((GraphQLQueryNode)n);
            }
            var query = new GraphQLQueryNode(schemaProvider, fragments, operation.Name, currentExpressionContext, (ParameterExpression)currentExpressionContext, rootFields, null);
            return query;
        }
        /// <summary>
        /// This is one of our top level node.
        /// mutation MyMutation {...}
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override IGraphQLBaseNode VisitMutationQuery(EntityGraphQLParser.MutationQueryContext context)
        {
            var operation = GetOperation(context.operationName());
            foreach (var item in operation.Arguments.Where(a => a.DefaultValue != null))
            {
                variables[item.ArgName] = Expression.Lambda(item.DefaultValue.Expression).Compile().DynamicInvoke();
            }
            this.currentExpressionContext = (ExpressionResult)Expression.Parameter(schemaProvider.ContextType, $"ctx");
            var mutateFields = new List<IGraphQLBaseNode>();
            foreach (var c in context.objectSelection().children)
            {
                var n = Visit(c);
                if (n != null)
                    mutateFields.Add(n);
            }
            var mutation = new GraphQLQueryNode(schemaProvider, fragments, operation.Name, null, null, mutateFields, null);
            return mutation;
        }

        public GraphQLOperation GetOperation(EntityGraphQLParser.OperationNameContext context)
        {
            if (context == null)
            {
                return new GraphQLOperation();
            }
            var visitor = new OperationVisitor(variables, schemaProvider);
            var op = visitor.Visit(context);

            return op;
        }

        public override IGraphQLBaseNode VisitGqlFragment(EntityGraphQLParser.GqlFragmentContext context)
        {
            // top level syntax part. Add to the fragrments and return null
            var typeName = context.fragmentType.GetText();
            currentExpressionContext = (ExpressionResult)Expression.Parameter(schemaProvider.Type(typeName).ContextType, $"frag_{typeName}");
            var fields = new List<IGraphQLBaseNode>();
            foreach (var item in context.fields.children)
            {
                var f = Visit(item);
                if (f != null) // white space etc
                    fields.Add(f);
            }
            fragments.Add(new GraphQLFragment(context.fragmentName.GetText(), fields, (ParameterExpression)currentExpressionContext));
            currentExpressionContext = null;
            return null;
        }

        public override IGraphQLBaseNode VisitFragmentSelect(EntityGraphQLParser.FragmentSelectContext context)
        {
            // top level syntax part. Add to the fragrments and return null
            var name = context.name.GetText();
            return new GraphQLFragmentSelect(name);
        }
    }
}

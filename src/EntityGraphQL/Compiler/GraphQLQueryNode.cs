using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents a field node in the GraphQL query. Below people, id, name, houses and location are all GraphQLQueryNodes.
    /// Only people and houses get ExecuteAsync() called as in the below example
    /// query MyQuery {
    ///     people { id, name },
    ///     houses { location }
    /// }
    /// Each of people and houses are seperate GraphQLNode expressions that can/will be executed e.g.
    /// (ctx) => ctx.People.Select(p => new {id = p.Id, name = p.Name })
    /// (ctx) => ctx.Houes.Select(p => new {location = p.Location })
    /// </summary>
    public class GraphQLQueryNode : GraphQLExecutableNode, IGraphQLBaseNode
    {
        /// <summary>
        /// Required as we build field selection at execution time
        /// </summary>
        private readonly ISchemaProvider schemaProvider;
        /// <summary>
        /// The Expression (usually a ParameterExpression or MemberExpression) used to build the Select object
        /// If the field is not IEnumerable e.g. param.Name, this is not used as the selection will be built using param.Name
        /// If the field is IEnumerable e.g. param.People, this will be a ParameterExpression of the element type of People.
        /// </summary>
        private readonly ExpressionResult selectionContext;
        /// <summary>
        /// A list of query fragments defined in the query document. Used to look up a fragment selection
        /// </summary>
        private readonly IEnumerable<GraphQLFragment> queryFragments;
        /// <summary>
        /// Holds the node's dotnet Expression
        /// </summary>
        private ExpressionResult fullNodeExpression;
        /// <summary>
        /// Holds the expression without any fields that use services
        /// </summary>
        private ExpressionResult nodeExpressionNoServiceFields;
        private Expression combineExpression;
        private bool hasWrappedService;
        private readonly List<IGraphQLBaseNode> nodeFields;

        /// <summary>
        /// Any values for a parameter that had a constant value in the query document.
        /// They are extracted out to parameters instead of inline ConstantExpression for future query caching possibilities
        /// </summary>
        private readonly Dictionary<ParameterExpression, object> constantParameters;
        private readonly List<Type> services = new List<Type>();
        /// <summary>
        /// List of services other than the context required to execute this node
        /// </summary>
        /// <value></value>
        public IEnumerable<Type> Services { get => services; }

        public string Name { get; set; }

        private readonly ExpressionResult fieldExpression;

        /// <summary>
        /// The root ParameterExpression context for this whole expression
        /// E.g. if the field is param.Name this will be param
        /// </summary>
        public ParameterExpression RootFieldParameter { get; set; }

        /// <summary>
        /// The fields that this node selects
        /// query {
        ///     rootField { queryField1 queryField2 ... }
        // }
        /// </summary>
        public IEnumerable<IGraphQLBaseNode> QueryFields { get => nodeFields; }

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters => constantParameters;

        /// <summary>
        /// Field is a complex expression (using a method or function) that returns a single object (not IEnumerable)
        /// We wrap this is a function that does a null check and avoid duplicate calls on the method/service
        /// </summary>
        /// <value></value>
        public bool HasAnyServices { get => hasWrappedService || QueryFields.Any(q => q.HasAnyServices) || Services?.Any() == true || QueryFields.Any(q => q.Services?.Any() == true); internal set => hasWrappedService = value; }

        /// <summary>
        /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
        /// </summary>
        /// <param name="schemaProvider">The schema provider used to build the expressions</param>
        /// <param name="queryFragments">Any query fragments</param>
        /// <param name="name">Name of the field. Could be the alais that the user provided</param>
        /// <param name="fieldExpression">The expression that makes the field. e.g. movie => movie.Name</param>
        /// <param name="fieldParameter">The ParameterExpression used for the field expression if required.</param>
        /// <param name="fieldSelection">Any fields that will be selected from this field e.g. (in GQL) { thisField { fieldSelection1 fieldSelection2 } }</param>
        /// <param name="selectionContext">The Expression used to build the fieldSelection expressions</param>
        public GraphQLQueryNode(ISchemaProvider schemaProvider, IEnumerable<GraphQLFragment> queryFragments, string name, ExpressionResult fieldExpression, ParameterExpression fieldParameter, IEnumerable<IGraphQLBaseNode> fieldSelection, ExpressionResult selectionContext)
        {
            Name = name;
            this.fieldExpression = fieldExpression;
            nodeFields = fieldSelection?.ToList() ?? new List<IGraphQLBaseNode>();
            this.schemaProvider = schemaProvider;
            this.queryFragments = queryFragments;
            this.selectionContext = selectionContext;
            this.RootFieldParameter = fieldParameter;
            constantParameters = new Dictionary<ParameterExpression, object>();
            services = new List<Type>();
            if (fieldExpression != null)
            {
                AddServices(fieldExpression.Services);
                foreach (var item in fieldExpression.ConstantParameters)
                {
                    constantParameters.Add(item.Key, item.Value);
                }
            }
            if (fieldSelection != null)
            {
                AddServices(fieldSelection.SelectMany(s => s.GetType() == typeof(GraphQLQueryNode) ? ((GraphQLQueryNode)s).Services : new List<Type>()));
                foreach (var item in fieldSelection.SelectMany(fs => fs.ConstantParameters))
                {
                    constantParameters.Add(item.Key, item.Value);
                }
            }
        }

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        public ExpressionResult GetNodeExpression(object contextValue, IServiceProvider serviceProvider, bool withoutServiceFields = false, ParameterExpression buildServiceWrapWithParam = null)
        {
            // we might have to build the expression on request as when we prase the query
            // document the fragment referenced might be defined later in the document
            // buildServiceWrapWithParam forces a rebuild as it is a new type for the selection including services
            if ((buildServiceWrapWithParam != null && fullNodeExpression != null) || (fullNodeExpression == null && nodeFields != null && nodeFields.Any()))
            {
                var replacer = new ParameterReplacer();
                var selectionFields = new Dictionary<string, IGraphQLBaseNode>();
                var selectionFieldsNoService = new Dictionary<string, IGraphQLBaseNode>();
                var isSelectOnList = fieldExpression.Type.IsEnumerableOrArray();
                var extractor = new ExpressionExtractor();

                foreach (var field in nodeFields)
                {
                    if (field is GraphQLFragmentSelect)
                    {
                        var fragment = queryFragments.FirstOrDefault(i => i.Name == field.Name);
                        if (fragment == null)
                            throw new EntityQuerySchemaException($"Fragment '{field.Name}' not found in query document");

                        foreach (IGraphQLBaseNode fragField in fragment.Fields)
                        {
                            var fieldExp = fragField.GetNodeExpression(contextValue, serviceProvider, false);
                            var exp = (ExpressionResult)replacer.Replace(fieldExp, fragment.SelectContext, selectionContext);
                            // new object as we reuse fragments
                            selectionFields[fragField.Name] = new GraphQLQueryNode(schemaProvider, queryFragments, fragField.Name, exp, selectionContext.AsParameter(), null, null);
                            if (fragField.HasAnyServices && withoutServiceFields)
                            {
                                extractor.Extract(exp, selectionContext.AsParameter()).ToList()
                                    .ForEach(e =>
                                    {
                                        IGraphQLBaseNode node = new GraphQLQueryNode(schemaProvider, null, e.Key, (ExpressionResult)e.Value, null, null, null);
                                        selectionFieldsNoService[fragField.Name] = node;
                                    });
                            }

                            // pull any constant values up
                            foreach (var item in fragField.ConstantParameters)
                            {
                                constantParameters.Add(item.Key, item.Value);
                            }
                            // pull up any services
                            AddServices(fieldExp.Services);
                        }
                    }
                    else
                    {
                        selectionFields[field.Name] = field;
                        if (field.HasAnyServices && withoutServiceFields)
                        {
                            // selectionFieldsNoService[field.Name] = field;
                            // if selectionContext isn't a ParameterExpression they are using a service as the context
                            // TODO - need to test more edge cases
                            if (selectionContext.AsParameter() != null)
                            {
                                var fieldExp = field.GetNodeExpression(contextValue, serviceProvider);
                                extractor.Extract(fieldExp, selectionContext.AsParameter()).ToList()
                                        .ForEach(e =>
                                        {
                                            IGraphQLBaseNode node = new GraphQLQueryNode(schemaProvider, null, e.Key, (ExpressionResult)e.Value, null, null, null);
                                            selectionFieldsNoService[field.Name] = node;
                                        });
                            }
                        }
                        AddServices(field.Services);
                    }
                }

                if (isSelectOnList)
                {
                    // build a .Select(...) - returning a IEnumerable<>
                    fullNodeExpression = (ExpressionResult)ExpressionUtil.MakeSelectWithDynamicType(selectionContext != null ? selectionContext.AsParameter() : RootFieldParameter, fieldExpression, selectionFields, serviceProvider, false, buildServiceWrapWithParam);
                    if (withoutServiceFields && selectionFieldsNoService.Any())
                        nodeExpressionNoServiceFields = (ExpressionResult)ExpressionUtil.MakeSelectWithDynamicType(selectionContext.AsParameter(), fieldExpression, selectionFieldsNoService, serviceProvider, withoutServiceFields);
                }
                else
                {
                    if (HasAnyServices)
                    {
                        // selectionFields is set up but we need to wrap
                        // we wrap here as we have access to the values and services etc
                        var fieldParamValues = new List<object>(ConstantParameters.Values);
                        var fieldParams = new List<ParameterExpression>(ConstantParameters.Keys);

                        var updatedExpression = InjectServices(serviceProvider, services, fieldParamValues, fieldExpression, fieldParams, replacer);

                        // we need to make sure the wrap can resolve any services in the select
                        var selectionExpressions = selectionFields.ToDictionary(f => f.Key, f => InjectServices(serviceProvider, f.Value.Services, fieldParamValues, f.Value.GetNodeExpression(contextValue, serviceProvider), fieldParams, replacer));
                        var originalParam = selectionFields.First().Value.FindRootParameterExpression();
                        // This is the var the we use in the select - the result of the service at runtime
                        buildServiceWrapWithParam = buildServiceWrapWithParam ?? originalParam;
                        var selectionParams = new List<ParameterExpression> { buildServiceWrapWithParam };
                        selectionParams.AddRange(selectionFields.Values.SelectMany(f => f.ConstantParameters.Keys));
                        var selectionParamValues = new List<object>(selectionFields.Values.SelectMany(f => f.ConstantParameters.Values));
                        selectionParamValues.AddRange(fieldParamValues);
                        selectionParams.AddRange(fieldParams);

                        selectionExpressions = selectionExpressions.ToDictionary(e => e.Key, e => (ExpressionResult)replacer.Replace(e.Value.Expression, originalParam, buildServiceWrapWithParam));

                        updatedExpression = ExpressionUtil.WrapFieldForNullCheck(updatedExpression, selectionParams.First(), selectionParams, selectionExpressions, selectionParamValues, selectionContext?.AsParameter());

                        fullNodeExpression = updatedExpression;
                    }
                    else
                    {
                        if (selectionFields.Any())
                        {
                            // build a new {...} - returning a single object {}
                            var newExp = ExpressionUtil.CreateNewExpression(selectionFields, serviceProvider, false, buildServiceWrapWithParam);
                            var anonType = newExp.Type;
                            // make a null check from this new expression
                            newExp = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, fieldExpression, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                            fullNodeExpression = (ExpressionResult)newExp;
                        }
                        if (withoutServiceFields && selectionFieldsNoService.Any())
                        {
                            var newExp = ExpressionUtil.CreateNewExpression(selectionFieldsNoService, serviceProvider, withoutServiceFields, buildServiceWrapWithParam);
                            var anonType = newExp.Type;

                            nodeExpressionNoServiceFields = (ExpressionResult)Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, fieldExpression, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                        }
                    }
                }
                foreach (var field in selectionFields.Values)
                {
                    foreach (var cp in field.ConstantParameters)
                    {
                        if (!constantParameters.ContainsKey(cp.Key))
                        {
                            constantParameters.Add(cp.Key, cp.Value);
                        }
                    }
                }
                if (combineExpression != null)
                {
                    var exp = (ExpressionResult)ExpressionUtil.CombineExpressions(fullNodeExpression, combineExpression);
                    exp.AddConstantParameters(fullNodeExpression.ConstantParameters);
                    exp.AddServices(fullNodeExpression.Services);
                    fullNodeExpression = exp;
                }
                fullNodeExpression?.AddServices(services);

                if (withoutServiceFields)
                    return nodeExpressionNoServiceFields;

                return fullNodeExpression;
            }
            else if (fullNodeExpression != null && nodeFields != null && nodeFields.Any())
            {
                if (withoutServiceFields)
                    return nodeExpressionNoServiceFields;

                return fullNodeExpression;
            }
            if (withoutServiceFields)
                return nodeExpressionNoServiceFields;
            return fieldExpression;
        }

        public void SetNodeExpression(ExpressionResult expr)
        {
            fullNodeExpression = expr;
        }

        /// <summary>
        /// Given this node (a field in the graphql object selection) we execute the expression against some data (context).
        /// This is only called for top level fields even though the object represents a field at any level
        /// e.g.
        /// query {
        ///    person { // a GraphQLQueryNode that will be executed
        ///       name // a GraphQLQueryNode that is not executed but rolled up into the one that is executed
        ///    }
        /// }
        /// </summary
        /// <param name="context"></param>
        /// <param name="validator"></param>
        /// <param name="serviceProvider"></param>
        /// <typeparam name="TContext"></typeparam>
        /// <returns></returns>
        public override Task<object> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider)
        {
            try
            {
                var lambdaExpression = Compile(context, serviceProvider, out List<object> allArgs);
                return Task.FromResult(lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray()));
            }
            catch (Exception)
            {
                throw new EntityGraphQLCompilerException($"Error executing query field {Name}");
            }
        }

        public LambdaExpression Compile<TContext>(TContext context, IServiceProvider serviceProvider, out List<object> allArgs)
        {
            allArgs = new List<object> { context };

            // should only be calling Execute at the top level
            var contextParam = RootFieldParameter;
            var parameters = new List<ParameterExpression> { contextParam };

            // For root/top level fields we need to first select the whole graph without fields that require services
            // so that EF Core 3.1+ can run and optimise the query
            // We then select the full graph from that context

            var replacer = new ParameterReplacer();
            // build this first as NodeExpression may modify ConstantParameters
            // this is without fields that require services - null if there are no service fields, just use the full expression
            var expression = GetNodeExpression(context, serviceProvider, withoutServiceFields: true);

            if (expression != null)
            {
                // the full selection is now on the anonymous type returned by the selection without fields. We don't know the type until now
                var newContext = Expression.Parameter(expression.Type);
                // call ToList() on top level nodes to force evaluation
                if (expression.Type.IsEnumerableOrArray())
                {
                    newContext = Expression.Parameter(expression.Type.GetEnumerableOrArrayType());
                    expression = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { newContext.Type }, expression);
                }
                // we now know the selection type without services and need to build the full select on that type
                // need to rebuild it
                var fullSelection = GetNodeExpression(context, serviceProvider, buildServiceWrapWithParam: newContext);

                // replace anything that required the normal root level param type with the new type selected above (without service fields)
                expression = CombineNodeExpressionWithSelection(expression, fullSelection, replacer, newContext);
            }
            else // just do things normally
            {
                // this is already evaluated and will just return the expression
                expression = GetNodeExpression(context, serviceProvider);
            }

            // this is the full requested graph
            // inject dependencies into the fullSelection
            if (services != null)
            {
                expression = InjectServices(serviceProvider, services, allArgs, expression, parameters, replacer);
            }

            if (constantParameters.Any())
            {
                foreach (var item in constantParameters)
                {
                    expression = (ExpressionResult)replacer.ReplaceByType(expression, item.Key.Type, item.Key);
                }
                parameters.AddRange(constantParameters.Keys);
                allArgs.AddRange(constantParameters.Values);
            }

            // evaluate everything
            if (expression.Type.IsEnumerableOrArray())
            {
                expression = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { expression.Type.GetEnumerableOrArrayType() }, expression);
            }

            var lambdaExpression = Expression.Lambda(expression, parameters.ToArray());
            return lambdaExpression;
        }

        /// <summary>
        /// Given a Expression that is the whole graph without service fields, combine it with the full selection graph
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="replacer"></param>
        /// <returns></returns>
        private ExpressionResult CombineNodeExpressionWithSelection(ExpressionResult expression, ExpressionResult fullNodeExpression, ParameterReplacer replacer, ParameterExpression newContextParam)
        {
            if (fullNodeExpression.NodeType != ExpressionType.Call)
                throw new Exception($"Unexpected NoteType {fullNodeExpression.NodeType}");

            var call = (MethodCallExpression)fullNodeExpression;
            if (call.Method.Name != "Select")
                throw new Exception($"Unexpected method {call.Method.Name}");

            var newExp = replacer.Replace(call.Arguments[1], QueryFields.First().RootFieldParameter, newContextParam);
            if (newExp.NodeType == ExpressionType.Quote)
                newExp = ((UnaryExpression)newExp).Operand;

            if (newExp.NodeType != ExpressionType.Lambda)
                throw new EntityGraphQLCompilerException($"Error compling query. Unexpected expression type {newExp.NodeType}");

            var selection = (LambdaExpression)newExp;
            var newCall = ExpressionUtil.MakeCallOnQueryable("Select", new Type[] { newContextParam.Type, selection.ReturnType }, expression, selection);
            return newCall;
        }

        private static ExpressionResult InjectServices(IServiceProvider serviceProvider, IEnumerable<Type> services, List<object> allArgs, ExpressionResult expression, List<ParameterExpression> parameters, ParameterReplacer replacer)
        {
            foreach (var serviceType in services.Distinct())
            {
                var srvParam = parameters.FirstOrDefault(p => p.Type == serviceType);
                if (srvParam == null)
                {
                    srvParam = Expression.Parameter(serviceType, $"srv_{serviceType.Name}");
                    parameters.Add(srvParam);
                    var service = serviceProvider.GetService(serviceType);
                    allArgs.Add(service);
                }

                expression = (ExpressionResult)replacer.ReplaceByType(expression, serviceType, srvParam);
            }

            return expression;
        }

        public void AddConstantParameter(ParameterExpression param, object val)
        {
            constantParameters.Add(param, val);
        }
        public void AddServices(IEnumerable<Type> services)
        {
            if (services == null)
                return;
            this.services.AddRange(services);
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={fullNodeExpression.ToString() ?? "not built yet"}";
        }

        public void SetCombineExpression(Expression combineExpression)
        {
            this.combineExpression = combineExpression;
        }

        public IEnumerable<IGraphQLBaseNode> GetFieldsWithoutServices(ParameterExpression contextParam)
        {
            if (!HasAnyServices && !QueryFields.Any())
            {
                return new List<IGraphQLBaseNode>
                {
                    new GraphQLQueryNode(schemaProvider, null, Name, fieldExpression, null, null, null)
                };
            }

            var extractor = new ExpressionExtractor();

            var fields = new List<IGraphQLBaseNode>();
            // if we're not selecting further, add this fields sans services
            if (!QueryFields.Any())
            {
                fields.AddRange(extractor.Extract(fieldExpression, contextParam)
                    .Select(e =>
                    {
                        IGraphQLBaseNode node = new GraphQLQueryNode(schemaProvider, null, e.Key, (ExpressionResult)e.Value, null, null, null);
                        return node;
                    }));
            }
            else
            {
                // Add the selection fields
                var newFieldExpression = extractor.Extract(fieldExpression, contextParam)?.FirstOrDefault().Value;
                if (newFieldExpression == null)
                    newFieldExpression = fieldExpression;
                fields.Add(new GraphQLQueryNode(schemaProvider, null, Name, (ExpressionResult)newFieldExpression, RootFieldParameter, QueryFields.SelectMany(q => q.GetFieldsWithoutServices(q.RootFieldParameter)), (ExpressionResult)contextParam));
            }
            return fields;
        }

        public ParameterExpression FindRootParameterExpression()
        {
            if (RootFieldParameter != null)
                return RootFieldParameter;
            return null;
        }
    }
}

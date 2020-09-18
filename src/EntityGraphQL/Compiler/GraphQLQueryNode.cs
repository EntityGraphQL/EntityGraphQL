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
    /// Represents a top level node in the GraphQL query.
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
        private ExpressionResult nodeExpression;
        private Expression combineExpression;
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
        /// The parameter that is required for executing this node's expression
        /// E.g. if the field is param.Name this will be param
        /// </summary>
        public ParameterExpression FieldParameter { get; set; }

        /// <summary>
        /// Used only at the top level of a query
        /// query {
        ///     queryField1 { field ... }
        ///     queryField2 { field ... }
        // }
        /// </summary>
        /// <typeparam name="GraphQLQueryNode"></typeparam>
        /// <returns></returns>
        public IEnumerable<IGraphQLBaseNode> QueryFields { get => nodeFields; }

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters => constantParameters;

        /// <summary>
        /// Field is a complex expression (using a method or function) that returns a single object (not IEnumerable)
        /// We wrap this is a function that does a null check and avoid duplicate calls on the method/service
        /// </summary>
        /// <value></value>
        public bool IsWrapped { get; internal set; }

        /// <summary>
        /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
        /// </summary>
        /// <param name="schemaProvider">The schema provider used to build the expressions</param>
        /// <param name="queryFragments">Any query fragments</param>
        /// <param name="name">Name of the field. Could be the alais that the user provided</param>
        /// <param name="fieldExpression">The expression that makes the field. e.g. movie => movie.Name</param>
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
            this.FieldParameter = fieldParameter;
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
        /// <value></value>
        public ExpressionResult GetNodeExpression(object contextValue, IServiceProvider serviceProvider)
        {
            // we might have to build the expression on request as when we prase the query
            // document the fragment referenced might be defined later in the document
            if (nodeExpression == null && nodeFields != null && nodeFields.Any())
            {
                var replacer = new ParameterReplacer();
                var selectionFields = new List<IGraphQLBaseNode>();
                var isSelectOnList = fieldExpression.Type.IsEnumerableOrArray();

                foreach (var field in nodeFields)
                {
                    if (field is GraphQLFragmentSelect)
                    {
                        var fragment = queryFragments.FirstOrDefault(i => i.Name == field.Name);
                        if (fragment == null)
                            throw new EntityQuerySchemaException($"Fragment '{field.Name}' not found in query document");

                        foreach (IGraphQLBaseNode fragField in fragment.Fields)
                        {
                            var fieldExp = fragField.GetNodeExpression(contextValue, serviceProvider);
                            var exp = (ExpressionResult)replacer.Replace(fieldExp, fragment.SelectContext, selectionContext);
                            // new object as we reuse fragments
                            selectionFields.Add(new GraphQLQueryNode(schemaProvider, queryFragments, fragField.Name, exp, selectionContext.AsParameter(), null, null));

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
                        selectionFields.Add(field);
                        AddServices(field.GetNodeExpression(contextValue, serviceProvider).Services);
                    }
                }

                if (isSelectOnList)
                {
                    // build a .Select(...) - returning a list<>
                    nodeExpression = (ExpressionResult)ExpressionUtil.SelectDynamicToList(selectionContext.AsParameter(), fieldExpression, selectionFields);
                }
                else
                {
                    if (IsWrapped)
                    {
                        // selectionFields is set up but we need to wrap
                        // we wrap here as we have access to the values and services etc
                        var fieldParamValues = new List<object> { contextValue };
                        fieldParamValues.AddRange(ConstantParameters.Values);
                        var fieldParams = new List<ParameterExpression> { FieldParameter };
                        fieldParams.AddRange(ConstantParameters.Keys);

                        var updatedExpression = InjectServices(contextValue, serviceProvider, fieldParamValues, fieldExpression, fieldParams, FieldParameter, replacer);

                        var selectionParams = new List<ParameterExpression> { selectionFields.First().FieldParameter };
                        selectionParams.AddRange(selectionFields.SelectMany(f => f.ConstantParameters.Keys));
                        var selectionParamValues = new List<object>(selectionFields.SelectMany(f => f.ConstantParameters.Values));
                        updatedExpression = ExpressionUtil.WrapFieldForNullCheck(updatedExpression, selectionParams, selectionFields, selectionParamValues);

                        nodeExpression = updatedExpression;
                    }
                    else
                    {
                        // build a new {...} - returning a single object {}
                        var newExp = ExpressionUtil.CreateNewExpression(selectionFields);
                        var anonType = newExp.Type;
                        // make a null check from this new expression
                        newExp = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, fieldExpression, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                        nodeExpression = (ExpressionResult)newExp;
                    }
                }
                foreach (var field in selectionFields)
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
                    var exp = (ExpressionResult)ExpressionUtil.CombineExpressions(nodeExpression, combineExpression);
                    exp.AddConstantParameters(nodeExpression.ConstantParameters);
                    exp.AddServices(nodeExpression.Services);
                    nodeExpression = exp;
                }
                nodeExpression.AddServices(services);
                return nodeExpression;
            }
            else if (nodeExpression != null && nodeFields != null && nodeFields.Any())
            {
                return nodeExpression;
            }
            return fieldExpression;
        }

        public void SetNodeExpression(ExpressionResult expr)
        {
            nodeExpression = expr;
        }

        public override Task<object> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider)
        {
            var allArgs = new List<object> { context };

            // build this first as NodeExpression may modify ConstantParameters
            var expression = GetNodeExpression(context, serviceProvider);

            // call tolist on top level nodes to force evaluation
            if (expression.Type.IsEnumerableOrArray())
            {
                expression = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { expression.Type.GetEnumerableOrArrayType() }, expression);
            }

            var parameters = new List<ParameterExpression> { FieldParameter };
            // should only be calling Execute at the top level
            var contextParam = FieldParameter;

            var replacer = new ParameterReplacer();
            // inject dependencies
            if (services != null)
            {
                expression = InjectServices(context, serviceProvider, allArgs, expression, parameters, contextParam, replacer);
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

            var lambdaExpression = Expression.Lambda(expression, parameters.ToArray());
            return Task.FromResult(lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray()));
        }

        private ExpressionResult InjectServices<TContext>(TContext context, IServiceProvider serviceProvider, List<object> allArgs, ExpressionResult expression, List<ParameterExpression> parameters, ParameterExpression contextParam, ParameterReplacer replacer)
        {
            foreach (var serviceType in services.Distinct())
            {
                if (serviceType == context.GetType())
                {
                    // inject the current context. As ReplaceByType will replace the context param too!
                    expression = (ExpressionResult)replacer.ReplaceByType(expression, serviceType, contextParam);
                }
                else
                {
                    var srvParam = Expression.Parameter(serviceType, $"srv_{serviceType.Name}");
                    expression = (ExpressionResult)replacer.ReplaceByType(expression, serviceType, srvParam);
                    parameters.Add(srvParam);
                    var service = serviceProvider.GetService(serviceType);
                    allArgs.Add(service);
                }
            }

            return expression;
        }

        public void AddConstantParameters(IReadOnlyDictionary<ParameterExpression, object> constantParameters)
        {
            foreach (var item in constantParameters)
            {
                this.constantParameters.Add(item.Key, item.Value);
            }
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
            return $"Node - Name={Name}, Expression={nodeExpression.ToString() ?? "not built yet"}";
        }

        public void AddField(IGraphQLBaseNode node)
        {
            nodeFields.Add(node);
            foreach (var item in node.ConstantParameters)
            {
                constantParameters.Add(item.Key, item.Value);
            }
        }

        public void SetCombineExpression(Expression combineExpression)
        {
            this.combineExpression = combineExpression;
        }
    }
}

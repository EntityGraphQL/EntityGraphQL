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
    /// Represents a top level node in the GraphQL query.
    /// query MyQuery {
    ///     people { id, name },
    ///     houses { location }
    /// }
    /// Each of people & houses are seperate GraphQLNode objects that can/will be executed
    /// </summary>
    public class GraphQLNode : IGraphQLNode
    {
        /// <summary>
        /// Required as we build field selection at execution time
        /// </summary>
        private readonly ISchemaProvider schemaProvider;
        /// <summary>
        /// Any fields we need to select. These could already be expression or a FragmentSelection Node
        /// </summary>
        private readonly IEnumerable<IGraphQLBaseNode> fieldSelection;
        /// <summary>
        /// The ParameterExpression used to build any of the field expression. We need to know this to replace it with the correct parameter
        /// </summary>
        private readonly ParameterExpression fieldParameter;
        /// <summary>
        /// A list of query fragments defined in the query document. Used to look up a fragment selection
        /// </summary>
        private readonly IEnumerable<GraphQLFragment> queryFragments;
        /// <summary>
        /// Holds the node's dotnet Expression
        /// </summary>
        private ExpressionResult nodeExpression;
        /// <summary>
        /// The base expression which the field seleciton is built on
        /// </summary>
        private readonly ExpressionResult fieldSelectionBaseExpression;
        private readonly List<IGraphQLNode> nodeFields;
        private readonly Dictionary<ParameterExpression, object> constantParameters;

        public string Name { get; private set; }
        public OperationType Type => OperationType.Query;

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        /// <value></value>
        public ExpressionResult GetNodeExpression()
        {
            // we might have to build the expression on request as when we prase the query
            // document the fragment referenced might be defined later in the document
            if (nodeExpression == null && fieldSelection != null && fieldSelection.Any())
            {
                var replacer = new ParameterReplacer();
                var selectionFields = new List<IGraphQLNode>();
                bool isSelect = fieldSelectionBaseExpression.Type.IsEnumerableOrArray();

                foreach (var field in fieldSelection)
                {
                    if (field is GraphQLFragmentSelect)
                    {
                        var fragment = queryFragments.FirstOrDefault(i => i.Name == field.Name);
                        if (fragment == null)
                            throw new EntityQuerySchemaException($"Fragment '{field.Name}' not found in query document");

                        foreach (IGraphQLNode fragField in fragment.Fields)
                        {
                            ExpressionResult exp = null;
                            if (isSelect)
                                exp = (ExpressionResult)replacer.Replace(fragField.GetNodeExpression(), fragment.SelectContext, fieldParameter);
                            else
                                exp = (ExpressionResult)replacer.Replace(fragField.GetNodeExpression(), fragment.SelectContext, fieldSelectionBaseExpression);
                            // new object as we reuse fragments
                            selectionFields.Add(new GraphQLNode(schemaProvider, queryFragments, fragField.Name, exp, null, null, null, null));

                            // pull any constant values up
                            foreach (var item in fragField.ConstantParameters)
                            {
                                constantParameters.Add(item.Key, item.Value);
                            }
                        }
                    }
                    else
                    {
                        var gfield = (IGraphQLNode)field;
                        selectionFields.Add(gfield);
                        // pull any constant values up
                        foreach (var item in gfield.ConstantParameters)
                        {
                            constantParameters.Add(item.Key, item.Value);
                        }
                    }
                }
                if (isSelect)
                {
                    // build a .Select(...) - returning a list<>
                    nodeExpression = (ExpressionResult)ExpressionUtil.SelectDynamicToList(fieldParameter, fieldSelectionBaseExpression, selectionFields, schemaProvider);
                }
                else
                {
                    // build a new {...} - returning a single object {}
                    var newExp = ExpressionUtil.CreateNewExpression(fieldSelectionBaseExpression, selectionFields, schemaProvider);
                    var anonType = newExp.Type;
                    // make a null check from this new expression
                    newExp = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, fieldSelectionBaseExpression, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                    nodeExpression = (ExpressionResult)newExp;
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

                foreach (var item in fieldSelectionBaseExpression.ConstantParameters)
                {
                    constantParameters.Add(item.Key, item.Value);
                }
            }
            return nodeExpression;
        }

        public void SetNodeExpression(ExpressionResult expr)
        {
            nodeExpression = expr;
        }

        /// <summary>
        /// The parameters that are required (should be passed in) for executing this node's expression
        /// </summary>
        /// <value></value>
        public List<ParameterExpression> Parameters { get; private set; }
        /// <summary>
        /// Any values for a parameter that had a constant value in the query document.
        /// They are extracted out to parameters instead of inline ConstantExpression for future query caching possibilities
        /// </summary>
        /// <value></value>
        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get => constantParameters; }

        public IEnumerable<IGraphQLNode> Fields { get => nodeFields; }

        public GraphQLNode(ISchemaProvider schemaProvider, IEnumerable<GraphQLFragment> queryFragments, string name, CompiledQueryResult query, ExpressionResult fieldSelectionBaseExpression) : this(schemaProvider, queryFragments, name, query.ExpressionResult, fieldSelectionBaseExpression, query.LambdaExpression.Parameters, null, null)
        {
            foreach (var item in query.ConstantParameters)
            {
                constantParameters.Add(item.Key, item.Value);
            }
        }

        public GraphQLNode(ISchemaProvider schemaProvider, IEnumerable<GraphQLFragment> queryFragments, string name, ExpressionResult exp, ExpressionResult fieldSelectionBaseExpression, IEnumerable<ParameterExpression> expressionParameters, IEnumerable<IGraphQLBaseNode> fieldSelection, ParameterExpression fieldParameter)
        {
            if (fieldSelectionBaseExpression == null && fieldSelection != null)
                throw new EntityGraphQLCompilerException($"fieldSelectionBaseExpression must be supplied for GraphQLNode if fieldSelection is supplied");

            Name = name;
            SetNodeExpression(exp);
            nodeFields = new List<IGraphQLNode>();
            this.schemaProvider = schemaProvider;
            this.queryFragments = queryFragments;
            this.fieldSelection = fieldSelection;
            this.fieldParameter = fieldParameter;
            this.fieldSelectionBaseExpression = fieldSelectionBaseExpression;
            Parameters = expressionParameters?.ToList();
            constantParameters = new Dictionary<ParameterExpression, object>();
            if (Parameters == null)
            {
                Parameters = new List<ParameterExpression>();
            }
        }

        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);

            // build this first as NodeExpression may modify ConstantParameters
            var expression = GetNodeExpression();

            // call tolist on top level nodes to force evaluation
            if (expression.Type.IsEnumerableOrArray())
            {
                expression = ExpressionUtil.MakeExpressionCall(new [] {typeof(Queryable), typeof(Enumerable)}, "ToList", new Type[] { expression.Type.GetEnumerableOrArrayType() }, expression);
            }

            var parameters = Parameters.ToList();
            if (ConstantParameters.Any())
            {
                var replacer = new ParameterReplacer();
                foreach (var item in ConstantParameters)
                {
                    expression = (ExpressionResult)replacer.ReplaceByType(expression, item.Key.Type, item.Key);
                }
                parameters.AddRange(ConstantParameters.Keys);
                allArgs.AddRange(ConstantParameters.Values);
            }
            var lambdaExpression = Expression.Lambda(expression, parameters.ToArray());
            return lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());
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
            this.constantParameters.Add(param, val);
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={GetNodeExpression()}";
        }

        public void AddField(IGraphQLNode node)
        {
            nodeFields.Add(node);
            foreach (var item in node.ConstantParameters)
            {
                constantParameters.Add(item.Key, item.Value);
            }
        }
    }
}

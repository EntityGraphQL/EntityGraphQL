using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
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
    /// Each of people & houses are seperate queries that can/will be executed
    /// </summary>
    public class GraphQLNode : IGraphQLNode
    {
        private readonly ISchemaProvider schemaProvider;
        private readonly IEnumerable<IGraphQLBaseNode> fieldSelection;
        private readonly ParameterExpression fieldParameter;
        private readonly IEnumerable<GraphQLFragment> queryFragments;
        private ExpressionResult nodeExpression;

        public string Name { get; private set; }
        public ExpressionResult NodeExpression {
            get
            {
                // we might have to build the expression on request as when we prase the query
                // document the fragment referenced might be defined later in the document
                if (nodeExpression == null && fieldSelection != null && fieldSelection.Any())
                {
                    var replacer = new ParameterReplacer();
                    var fields = new List<IGraphQLNode>();
                    bool isSelect = RelationExpression.Type.IsEnumerableOrArray();

                    foreach (var field in fieldSelection)
                    {
                        if (field is GraphQLFragmentSelect)
                        {
                            var fragment = queryFragments.FirstOrDefault(i => i.Name == field.Name);
                            if (fragment == null)
                                throw new EntityQuerySchemaError($"Fragment '{field.Name}' not found in query document");

                            foreach (IGraphQLNode fragField in fragment.Fields)
                            {
                                ExpressionResult exp = null;
                                if (isSelect)
                                    exp = (ExpressionResult)replacer.Replace(fragField.NodeExpression, fragment.SelectContext, fieldParameter);
                                else
                                    exp = (ExpressionResult)replacer.Replace(fragField.NodeExpression, fragment.SelectContext, RelationExpression);
                                // new object as we reuse fragments
                                fields.Add(new GraphQLNode(schemaProvider, queryFragments, fragField.Name, exp, null, null, fragField.ConstantParameterValues, null, null));
                            }
                        }
                        else
                        {
                            fields.Add((IGraphQLNode)field);
                        }
                    }
                    if (isSelect)
                    {
                        // build a .Select(...)
                        nodeExpression = (ExpressionResult)ExpressionUtil.SelectDynamic(fieldParameter, RelationExpression, fields, schemaProvider);
                    }
                    else
                    {
                        // build a new {...}
                        var newExp = ExpressionUtil.CreateNewExpression(RelationExpression, fields, schemaProvider);
                        var anonType = newExp.Type;
                        // make a null check from this new expression
                        // if (!rootField.IsMutation)
                        // {
                            newExp = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, RelationExpression, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                        // }
                        nodeExpression = (ExpressionResult)newExp;
                    }
                }
                return nodeExpression;
            }
            set => nodeExpression = value;
        }
        public List<ParameterExpression> Parameters { get; private set; }
        public List<object> ConstantParameterValues { get; private set; }

        public List<IGraphQLNode> Fields { get; private set; }
        public Expression RelationExpression { get; private set; }

        public GraphQLNode(ISchemaProvider schemaProvider, IEnumerable<GraphQLFragment> queryFragments, string name, CompiledQueryResult query, Expression relationExpression) : this(schemaProvider, queryFragments, name, (ExpressionResult)query.ExpressionResult, relationExpression, query.LambdaExpression.Parameters, query.ConstantParameterValues, null, null)
        {
        }

        public GraphQLNode(ISchemaProvider schemaProvider, IEnumerable<GraphQLFragment> queryFragments, string name, ExpressionResult exp, Expression relationExpression, IEnumerable<ParameterExpression> expressionParameters, IEnumerable<object> constantParameterValues, IEnumerable<IGraphQLBaseNode> fieldSelection, ParameterExpression fieldParameter)
        {
            this.schemaProvider = schemaProvider;
            this.queryFragments = queryFragments;
            Name = name;
            NodeExpression = exp;
            this.fieldSelection = fieldSelection;
            this.fieldParameter = fieldParameter;
            Fields = new List<IGraphQLNode>();
            if (relationExpression != null)
            {
                RelationExpression = relationExpression;
            }
            Parameters = expressionParameters?.ToList();
            ConstantParameterValues = constantParameterValues?.ToList();
        }

        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (ConstantParameterValues != null && ConstantParameterValues.Any())
            {
                allArgs.AddRange(ConstantParameterValues);
            }

            var lambdaExpression = Expression.Lambda(NodeExpression, Parameters.ToArray());
            return lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={NodeExpression}";
        }
    }
}

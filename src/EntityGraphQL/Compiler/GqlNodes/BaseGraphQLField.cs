using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Once we parse the GQL document we get a graph of BaseGraphQLField objects where each one is ObjectProjectionField
    /// ListSelectionField, ScalarField or FragmentField. Example of each below in a GQL document
    /// {
    ///     singleEntity { # ObjectProjectionField
    ///         this # ScalarField
    ///         that # ScalarField
    ///     }
    ///     listOfThings { # ListSelectionField
    ///         ...someFrag # FragmentField
    ///     }
    /// }
    /// </summary>
    public abstract class BaseGraphQLField : IGraphQLNode
    {
        protected List<IFieldExtension> fieldExtensions;
        public Expression NextContextExpression { get; set; }
        public IGraphQLNode ParentNode { get; set; }
        public ParameterExpression RootParameter { get; set; }

        /// <summary>
        /// Name of the field
        /// </summary>
        /// <value></value>
        public string Name { get; protected set; }

        public BaseGraphQLField(string name, Expression nextContextExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
        {
            Name = name;
            NextContextExpression = nextContextExpression;
            RootParameter = rootParameter;
            ParentNode = parentNode;
        }

        /// <summary>
        /// Any values for a parameter that had a constant value in the query document.
        /// They are extracted out to parameters instead of inline ConstantExpression for future query caching possibilities
        /// </summary>
        protected Dictionary<ParameterExpression, object> constantParameters = new();
        public List<BaseGraphQLField> QueryFields { get; } = new();
        internal Dictionary<ParameterExpression, object> ConstantParameters { get => constantParameters; }
        public List<Type> Services { get; } = new List<Type>();
        /// <summary>
        /// Field is a complex expression (using a method or function) that returns a single object (not IEnumerable)
        /// We wrap this is a function that does a null check and avoid duplicate calls on the method/service
        /// </summary>
        /// <value></value>
        public virtual bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return Services?.Any() == true || QueryFields?.Any(f => f.HasAnyServices(fragments)) == true;
        }
        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        /// <param name="serviceProvider">Service provider to resolve services </param>
        /// <param name="fragments">Fragments in the query document</param>
        /// <param name="withoutServiceFields">If true th expression builds selection without fields that require services</param>
        /// <param name="replaceContextWith">A replacement context from a selection without service fields</param>
        /// <param name="isRoot">If this field is a Query root field</param>
        /// <param name="useReplaceContextDirectly">Use the replaceContextWith instead of running through replacer. Used for fields gone from collection to single when running services seperately</param>
        /// <returns></returns>
        public abstract Expression GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression schemaContext, bool withoutServiceFields, Expression replaceContextWith = null, bool isRoot = false, bool useReplaceContextDirectly = false);

        public abstract IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields);

        public void AddServices(IEnumerable<Type> services)
        {
            if (services == null)
                return;
            Services.AddRange(services);
        }

        public void AddField(BaseGraphQLField field)
        {
            QueryFields.Add(field);
            AddServices(field.GetType() == typeof(GraphQLListSelectionField) ? ((GraphQLListSelectionField)field).Services : new List<Type>());
            foreach (var item in field.ConstantParameters)
            {
                constantParameters.Add(item.Key, item.Value);
            }
        }

        protected (Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExtensionsSelection(GraphQLFieldType fieldType, Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, ParameterReplacer parameterReplacer)
        {
            if (fieldExtensions != null)
            {
                foreach (var extension in fieldExtensions)
                {
                    (baseExpression, selectionExpressions, selectContextParam) = extension.ProcessExpressionSelection(fieldType, baseExpression, selectionExpressions, selectContextParam, parameterReplacer);
                }
            }
            return (baseExpression, selectionExpressions, selectContextParam);
        }
        protected Expression ProcessScalarExpression(Expression expression, ParameterReplacer parameterReplacer)
        {
            if (fieldExtensions != null)
            {
                foreach (var extension in fieldExtensions)
                {
                    expression = extension.ProcessScalarExpression(expression, parameterReplacer);
                }
            }
            return expression;
        }

        internal void AddConstantParameters(IReadOnlyDictionary<ParameterExpression, object> constantParameters)
        {
            foreach (var item in constantParameters)
            {
                this.constantParameters.Add(item.Key, item.Value);
            }
        }
    }
}
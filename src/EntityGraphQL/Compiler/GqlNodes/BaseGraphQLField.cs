using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
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
        public Expression? NextFieldContext { get; set; }
        public IGraphQLNode? ParentNode { get; set; }
        public ParameterExpression? RootParameter { get; set; }
        /// <summary>
        /// Arguments from inline in the query
        /// </summary>
        protected readonly Dictionary<string, Expression> arguments;

        /// <summary>
        /// Name of the field
        /// </summary>
        /// <value></value>
        public string Name { get; protected set; }
        protected IField? field;

        public BaseGraphQLField(string name, Expression? nextFieldContext, ParameterExpression? rootParameter, IGraphQLNode? parentNode, Dictionary<string, Expression>? arguments)
        {
            Name = name;
            NextFieldContext = nextFieldContext;
            RootParameter = rootParameter;
            ParentNode = parentNode;
            this.arguments = arguments ?? new Dictionary<string, Expression>();
            fieldExtensions = new List<IFieldExtension>();
        }

        /// <summary>
        /// Any values for a parameter that had a constant value in the query document.
        /// They are extracted out to parameters instead of inline ConstantExpression for future query caching possibilities
        /// </summary>
        protected Dictionary<ParameterExpression, object> constantParameters = new();
        public List<BaseGraphQLField> QueryFields { get; } = new();
        internal Dictionary<ParameterExpression, object> ConstantParameters { get => constantParameters; }
        public List<Type> Services { get; set; } = new List<Type>();
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
        /// <param name="parentArguments">Inline arguments from the parent fields. May be needed when a Field Extension rebuilds the schema shape</param>
        /// <param name="withoutServiceFields">If true the expression builds without fields that require services</param>
        /// <param name="replacementNextFieldContext">A replacement context from a selection without service fields</param>
        /// <param name="isRoot">If this field is a Query root field</param>
        /// <param name="contextChanged">If true the context has changed. This means we are compiling/executing against the result ofa pre-selection without service fields</param>
        /// <returns></returns>
        public abstract Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Dictionary<string, Expression> parentArguments, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false);

        public abstract IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields);

        public void AddServices(IEnumerable<Type>? services)
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

        protected (Expression, ParameterExpression?) ProcessExtensionsPreSelection(GraphQLFieldType fieldType, Expression baseExpression, ParameterExpression? listTypeParam, ParameterReplacer parameterReplacer)
        {
            if (fieldExtensions != null)
            {
                foreach (var extension in fieldExtensions)
                {
                    (baseExpression, listTypeParam) = extension.ProcessExpressionPreSelection(fieldType, baseExpression, listTypeParam, parameterReplacer);
                }
            }
            return (baseExpression, listTypeParam);
        }

        protected (Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression? selectContextParam) ProcessExtensionsSelection(GraphQLFieldType fieldType, Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression? selectContextParam, bool servicesPass, ParameterReplacer parameterReplacer)
        {
            if (fieldExtensions != null)
            {
                foreach (var extension in fieldExtensions)
                {
                    (baseExpression, selectionExpressions, selectContextParam) = extension.ProcessExpressionSelection(fieldType, baseExpression, selectionExpressions, selectContextParam, servicesPass, parameterReplacer);
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
            if (constantParameters == null)
                return;
            foreach (var item in constantParameters)
            {
                if (!this.constantParameters.ContainsKey(item.Key))
                    this.constantParameters.Add(item.Key, item.Value);
            }
        }
    }
}
using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

public class FilterExpressionExtension : BaseFieldExtension
{
    private bool isQueryable;
    private Type? listType;

    /// <summary>
    /// Configure the field for a filter argument. Do as much as we can here as it is only executed once.
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="field"></param>
    public override void Configure(ISchemaProvider schema, IField field)
    {
        if (field.ResolveExpression == null)
            throw new EntityGraphQLSchemaException($"FilterExpressionExtension requires a Resolve function set on the field");

        if (!field.ResolveExpression.Type.IsEnumerableOrArray())
            throw new EntityGraphQLSchemaException($"Expression for field {field.Name} must be a collection to use FilterExpressionExtension. Found type {field.ReturnType.TypeDotnet}");

        listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()!;

        // Update field arguments
        var args = Activator.CreateInstance<FilterArgs>()!;
        field.AddArguments(args);

        isQueryable = typeof(IQueryable).IsAssignableFrom(field.ResolveExpression.Type);
    }

    public override (Expression? expression, ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue) GetExpressionAndArguments(
        IField field,
        BaseGraphQLField fieldNode,
        Expression expression,
        ParameterExpression? argumentParam,
        dynamic? arguments,
        Expression context,
        bool servicesPass,
        ParameterReplacer parameterReplacer,
        ParameterExpression? originalArgParam,
        CompileContext compileContext
    )
    {
        var filter = arguments?.Filter as EntityQueryType;
        if (arguments != null && filter != null && filter?.HasValue)
        {
            // Ensure the filter Expression is compiled at this point if only raw text was provided earlier
            if (filter!.Query == null && !string.IsNullOrWhiteSpace(filter.Text))
            {
                try
                {
                    var eqlContext = new EqlCompileContext(compileContext);
                    var compiled = ExpressionUtil.BuildEntityQueryExpression(field.Schema, listType!, filter.Text!, eqlContext, fieldNode.NextFieldContext as ParameterExpression);
                    // Set back the compiled lambda to the arguments.Filter.Query property
                    filter.Query = (LambdaExpression)compiled;
                    filter.ServiceFieldDependencies = eqlContext.ServiceFieldDependencies;
                    filter.OriginalContext = eqlContext.OriginalContext;
                }
                catch (EntityGraphQLException ex)
                {
                    throw new EntityGraphQLException(ex.Category, $"Field '{fieldNode.Name}' - {ex.Message}");
                }
            }

            var filterExpression = filter.Query!;

            if (compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately)
            {
                // Split filter into EF-safe and service-dependent parts
                var splitter = new FilterSplitter(listType!);
                var split = splitter.SplitFilter(filterExpression);

                if (!servicesPass && split.NonServiceFilter != null)
                {
                    expression = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Where", [expression.Type.GetEnumerableOrArrayType()!], expression, split.NonServiceFilter);
                }
                else if (servicesPass && split.ServiceFilter != null)
                {
                    var newListType = expression.Type.GetGenericArguments()[0];
                    Expression filterExpressionServices = split.ServiceFilter.Body;
                    var filterContext = Expression.Parameter(newListType, "ctx_filter_services");
                    foreach (var item in filter.ServiceFieldDependencies)
                    {
                        var extractedFields = item.ExtractedFieldsFromServices ?? [];
                        var expReplacer = new ExpressionReplacer(extractedFields, filterContext, false, false, [newListType]);
                        filterExpressionServices = expReplacer.Replace(filterExpressionServices);
                    }
                    // filter might have non service fields in it that need the parameter replaced
                    filterExpressionServices = parameterReplacer.Replace(filterExpressionServices, filter.OriginalContext!, filterContext);
                    filterExpressionServices = Expression.Lambda(filterExpressionServices, filterContext);
                    expression = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Where", [newListType], expression, filterExpressionServices);
                }
            }
            else
            {
                // Single pass execution - apply full filter
                expression = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Where", [expression.Type.GetEnumerableOrArrayType()!], expression, filterExpression);
            }
        }

        return (expression, originalArgParam, argumentParam, arguments);
    }
}

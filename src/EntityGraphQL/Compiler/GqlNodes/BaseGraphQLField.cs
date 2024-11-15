using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

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
public abstract class BaseGraphQLField : IGraphQLNode, IFieldKey
{
    public ExecutableDirectiveLocation LocationForDirectives { get; protected set; } = ExecutableDirectiveLocation.FIELD;
    public ISchemaProvider Schema { get; protected set; }
    protected List<GraphQLDirective> Directives { get; set; } = [];
    protected string? OpName { get; set; }
    public virtual bool IsRootField { get; set; }

    /// <summary>
    /// Name of the field
    /// </summary>
    public string Name { get; set; }
    public string SchemaName => Field?.Name ?? Name;

    /// <summary>
    /// The GraphQL type this field belongs to. Useful with union types and inline fragments and we may have the same name
    /// across types. E.g name field below
    /// {
    ///     animals {
    ///         __typename
    ///         ... on Dog {
    ///             name
    ///             hasBone
    ///         }
    ///         ... on Cat {
    ///             name
    ///             lives
    ///         }
    ///     }
    /// }
    /// </summary>
    public ISchemaType? FromType => Field?.FromType;
    public IField? Field { get; }
    public List<BaseGraphQLField> QueryFields { get; } = [];
    public Expression? NextFieldContext { get; }
    public IGraphQLNode? ParentNode { get; set; }

    public ParameterExpression? RootParameter { get; set; }

    /// <summary>
    /// Arguments from inline in the query - not $ variables
    /// </summary>
    public IReadOnlyDictionary<string, object> Arguments { get; }

    /// <summary>
    /// True if this field directly has services
    /// </summary>
    public bool HasServices => Field?.Services.Count > 0;

    public BaseGraphQLField(
        ISchemaProvider schema,
        IField? field,
        string name,
        Expression? nextFieldContext,
        ParameterExpression? rootParameter,
        IGraphQLNode? parentNode,
        IReadOnlyDictionary<string, object>? arguments
    )
    {
        Name = name;
        NextFieldContext = nextFieldContext;
        RootParameter = rootParameter;
        ParentNode = parentNode;
        this.Arguments = arguments ?? new Dictionary<string, object>();
        this.Schema = schema;
        Field = field;
    }

    public BaseGraphQLField(BaseGraphQLField context, Expression? nextFieldContext)
    {
        Name = context.Name;
        NextFieldContext = nextFieldContext ?? context.NextFieldContext;
        RootParameter = context.RootParameter;
        ParentNode = context.ParentNode;
        Arguments = context.Arguments.ToDictionary(k => k.Key, v => v.Value);
        Schema = context.Schema;
        Field = context.Field;
        LocationForDirectives = context.LocationForDirectives;
        Directives.AddRange(context.Directives);
        QueryFields.AddRange(context.QueryFields);
        OpName = context.OpName;
        IsRootField = context.IsRootField;
    }

    /// <summary>
    /// Field is a complex expression (using a method or function) that returns a single object (not IEnumerable)
    /// We wrap this is a function that does a null check and avoid duplicate calls on the method/service
    /// </summary>
    /// <value></value>
    public virtual bool HasServicesAtOrBelow(IEnumerable<GraphQLFragmentStatement> fragments)
    {
        return Field?.Services.Count > 0 || QueryFields.Any(f => f.HasServicesAtOrBelow(fragments));
    }

    /// <summary>
    /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
    /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
    /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
    /// Execute() so we can look up any query fragment selections
    /// </summary>
    /// <param name="serviceProvider">Service provider to resolve services </param>
    /// <param name="fragments">Fragments in the query document</param>
    /// <param name="docParam">ParameterExpression for the variables defined in the request document</param>
    /// <param name="docVariables">Resolved values of the variables passed in the request document</param>
    /// <param name="schemaContext">ParameterExpression of the schema's Query context</param>
    /// <param name="withoutServiceFields">If true the expression builds without fields that require services</param>
    /// <param name="replacementNextFieldContext">A replacement context from a selection without service fields</param>
    /// <param name="contextChanged">If true the context has changed. This means we are compiling/executing against the result ofa pre-selection without service fields</param>
    /// <param name="replacer">Replace used to make changes to expressions</param>
    /// <returns></returns>
    public Expression? GetNodeExpression(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        object? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        IGraphQLNode? fieldNode = ProcessDirectivesVisitNode(LocationForDirectives, this, docParam, docVariables);

        if (fieldNode == null)
            return null;

        CheckFieldAccess(Schema, Field, compileContext.RequestContext);

        return ((BaseGraphQLField)fieldNode).GetFieldExpression(
            compileContext,
            serviceProvider,
            fragments,
            docParam,
            docVariables,
            schemaContext,
            withoutServiceFields,
            replacementNextFieldContext,
            possibleNextContextTypes,
            contextChanged,
            replacer
        );
    }

    /// <summary>
    /// GetNodeExpression but without the directive processing as we do not want to process continuously
    /// </summary>
    protected abstract Expression? GetFieldExpression(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        object? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    );

    public IEnumerable<BaseGraphQLField> Expand(
        CompileContext compileContext,
        List<GraphQLFragmentStatement> fragments,
        bool withoutServiceFields,
        Expression fieldContext,
        ParameterExpression? docParam,
        object? docVariables
    )
    {
        IGraphQLNode? fieldNode = ProcessDirectivesVisitNode(LocationForDirectives, this, docParam, docVariables);

        if (fieldNode == null)
            return new List<BaseGraphQLField>();

        return ((BaseGraphQLField)fieldNode).ExpandField(compileContext, fragments, withoutServiceFields, fieldContext, docParam, docVariables);
    }

    protected virtual IEnumerable<BaseGraphQLField> ExpandField(
        CompileContext compileContext,
        List<GraphQLFragmentStatement> fragments,
        bool withoutServiceFields,
        Expression fieldContext,
        ParameterExpression? docParam,
        object? docVariables
    )
    {
        return ExpandFromServices(withoutServiceFields, this);
    }

    /// <summary>
    /// Bring up any context based expression from services
    /// </summary>
    /// <returns></returns>
    internal virtual IEnumerable<BaseGraphQLField> ExpandFromServices(bool withoutServiceFields, BaseGraphQLField? field)
    {
        if (withoutServiceFields && Field?.ExtractedFieldsFromServices != null)
            return Field.ExtractedFieldsFromServices.ToList();

        return withoutServiceFields && HasServices ? [] : new List<BaseGraphQLField> { field ?? this };
    }

    public void AddField(BaseGraphQLField field)
    {
        QueryFields.Add(field);
    }

    protected (Expression, ParameterExpression?) ProcessExtensionsPreSelection(Expression baseExpression, ParameterExpression? listTypeParam, ParameterReplacer parameterReplacer)
    {
        if (Field == null)
            return (baseExpression, listTypeParam);
        foreach (var extension in Field.Extensions)
        {
            (baseExpression, listTypeParam) = extension.ProcessExpressionPreSelection(baseExpression, listTypeParam, parameterReplacer);
        }
        return (baseExpression, listTypeParam);
    }

    protected (Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam) ProcessExtensionsSelection(
        Expression baseExpression,
        Dictionary<IFieldKey, CompiledField> selectionExpressions,
        ParameterExpression? selectContextParam,
        ParameterExpression? argumentParam,
        bool servicesPass,
        ParameterReplacer parameterReplacer
    )
    {
        if (Field == null)
            return (baseExpression, selectionExpressions, selectContextParam);
        foreach (var extension in Field.Extensions)
        {
            (baseExpression, selectionExpressions, selectContextParam) = extension.ProcessExpressionSelection(
                baseExpression,
                selectionExpressions,
                selectContextParam,
                argumentParam,
                servicesPass,
                parameterReplacer
            );
        }
        return (baseExpression, selectionExpressions, selectContextParam);
    }

    protected Expression ProcessScalarExpression(Expression expression, ParameterReplacer parameterReplacer)
    {
        if (Field == null)
            return expression;
        foreach (var extension in Field.Extensions)
        {
            expression = extension.ProcessScalarExpression(expression, parameterReplacer);
        }
        return expression;
    }

    public void AddDirectives(IEnumerable<GraphQLDirective> graphQLDirectives)
    {
        Directives.AddRange(graphQLDirectives);
    }

    protected IGraphQLNode? ProcessDirectivesVisitNode(ExecutableDirectiveLocation location, BaseGraphQLField field, ParameterExpression? docParam, object? docVariables)
    {
        IGraphQLNode? result = field;
        foreach (var directive in Directives)
        {
            result = directive.VisitNode(location, Schema, field, Arguments, docParam, docVariables);
        }
        return result;
    }

    protected Expression ReplaceContext(Expression replacementNextFieldContext, ParameterReplacer replacer, Expression nextFieldContext, List<Type>? possibleNextContextTypes)
    {
        var possibleField = replacementNextFieldContext.Type.GetField(Name);
        if (possibleField != null)
            nextFieldContext = Expression.Field(replacementNextFieldContext, possibleField);
        else // need to replace context expressions in the service expression with the new context
        {
            // If this is a root field, we replace the whole expression unless there is services at the root level
            if (IsRootField && !HasServices)
                nextFieldContext = replacementNextFieldContext;
            else if (HasServices)
            {
                // if we have services we need to replace any context expressions in the service expression with the new context
                var expressionsToReplace = ExpandFromServices(true, null).Cast<GraphQLExtractedField>();
                // e.g. given a field like this
                // (ctx, service) => service.DoSomething(ctx.SomeField)
                // we selected ctx.SomeField on the first execution and on the second execution we use newCtx.ctx_SomeField
                // if ParentNode?.HasServices == true the above has been done and we just need to replace the
                // expression, not rebuild it with a different name

                var expReplacer = new ExpressionReplacer(expressionsToReplace, replacementNextFieldContext, ParentNode?.HasServices == true, IsRootField && HasServices, possibleNextContextTypes);
                nextFieldContext = expReplacer.Replace(nextFieldContext!);
            }
            // may need to replace the field's original parameter
            if (Field?.FieldParam != null)
            {
                nextFieldContext = replacer.Replace(nextFieldContext, Field.FieldParam, replacementNextFieldContext);
            }
        }

        return nextFieldContext;
    }

    protected Expression? HandleBulkServiceResolver(CompileContext compileContext, bool withoutServiceFields, Expression? nextFieldContext)
    {
        if (Field?.BulkResolver != null && (ParentNode as BaseGraphQLQueryField)?.ToSingleNode == null)
        {
            if (!withoutServiceFields)
            {
                // we replace the expression with a lookup in the bulk resolver data
                // e.g. bulkData[compileContext.BulkResolvers.Name][field.Field.BulkResolver.DataSelector]
                var expression = Expression.MakeIndex(compileContext.BulkParameter!, typeof(Dictionary<string, object>).GetProperty("Item")!, new[] { Expression.Constant(Field.BulkResolver.Name) });
                var dictType = typeof(Dictionary<,>).MakeGenericType(Field.BulkResolver.DataSelector.ReturnType, Field.ReturnType.TypeDotnet);
                nextFieldContext = Expression.MakeIndex(Expression.Convert(expression, dictType), dictType.GetProperty("Item")!, new[] { Field!.BulkResolver.DataSelector.Body });
                nextFieldContext = Expression.Convert(nextFieldContext, Field.ReturnType.TypeDotnet);
            }
        }

        return nextFieldContext;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode() + SchemaName.GetHashCode() + FromType?.GetHashCode() ?? 0;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as BaseGraphQLField);
    }

    public bool Equals(BaseGraphQLField? obj)
    {
        return obj != null && obj.Name == this.Name && SchemaName == obj.SchemaName && obj.FromType?.Name == this.FromType?.Name;
    }

    public static void HandleBeforeRootFieldExpressionBuild(CompileContext compileContext, string? opName, string fieldName, bool contextChanged, bool isRootField, ref Expression expression)
    {
        if (compileContext.ExecutionOptions.BeforeRootFieldExpressionBuild != null && !contextChanged && isRootField)
        {
            var currentReturnType = expression.Type;
            expression = compileContext.ExecutionOptions.BeforeRootFieldExpressionBuild(expression, opName, fieldName);
            if (expression.Type != currentReturnType && !expression.Type.IsAssignableFrom(currentReturnType))
                throw new EntityGraphQLCompilerException($"BeforeExpressionBuild changed the return type from {currentReturnType} to {expression.Type}");
        }
    }

    public static string? GetOperationName(BaseGraphQLField node)
    {
        if (node.OpName != null)
            return node.OpName;
        if (node.ParentNode is ExecutableGraphQLStatement opNode)
            return node.OpName = opNode.Name;
        if (node.ParentNode != null && node.ParentNode is BaseGraphQLField field)
        {
            node.OpName = GetOperationName(field);
            return node.OpName;
        }
        return null;
    }

    /// <summary>
    /// Throws exception if the user does not have access to the field or the return type
    /// </summary>
    /// <param name="requestContext"></param>
    internal static void CheckFieldAccess(ISchemaProvider schema, IField? fieldNode, QueryRequestContext requestContext)
    {
        if (fieldNode == null)
            return;

        var field = fieldNode.FromType?.GetField(fieldNode.Name, requestContext);
        if (field != null)
        {
            // check type
            schema.CheckTypeAccess(field.ReturnType.SchemaType, requestContext);
        }
    }
}

public interface IFieldKey
{
    /// <summary>
    /// Name of the field. May be an alias
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Name of the field as it appears in the schema
    /// </summary>
    string SchemaName { get; }

    /// <summary>
    /// The GraphQL type this field belongs to. Useful with union types and inline fragments and we may have the same name
    /// across types. E.g name field below
    /// {
    ///     animals {
    ///         __typename
    ///         ... on Dog {
    ///             name
    ///             hasBone
    ///         }
    ///         ... on Cat {
    ///             name
    ///             lives
    ///         }
    ///     }
    /// }
    /// </summary>
    ISchemaType? FromType { get; }
}

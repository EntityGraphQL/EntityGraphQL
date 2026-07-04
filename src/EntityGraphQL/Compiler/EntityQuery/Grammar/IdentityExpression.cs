using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

public class IdentityExpression(string name, EqlCompileContext compileContext) : IExpression
{
    public Type Type => throw new NotImplementedException();

    public string Name { get; } = name;

    public Expression Compile(Expression? context, EntityQueryParser parser, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        return MakePropertyCall(context!, schema, Name, requestContext, compileContext);
    }

    internal static Expression MakePropertyCall(Expression context, ISchemaProvider? schema, string name, QueryRequestContext requestContext, EqlCompileContext compileContext)
    {
        if (schema == null)
        {
            try
            {
                return Expression.PropertyOrField(context!, name);
            }
            catch (Exception)
            {
                return MakeConstantFromIdentity(context, schema, name, requestContext);
            }
        }

        // the context is the underlying collection of a paged field (see below). The Page/Connection wrapper
        // fields only exist in the GraphQL schema - map them onto the collection
        if (compileContext.PagedFieldExpressions.Contains(context))
        {
            if (string.Equals(name, "items", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "edges", StringComparison.OrdinalIgnoreCase))
                return context; // identity - the collection is the items
            if (string.Equals(name, "totalItems", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "totalCount", StringComparison.OrdinalIgnoreCase))
            {
                var elementType = context.Type.GetEnumerableOrArrayType()!;
                return Expression.Call(context.Type.IsGenericTypeQueryable() ? typeof(Queryable) : typeof(Enumerable), nameof(Enumerable.Count), [elementType], context);
            }
            throw new EntityGraphQLException(
                GraphQLErrorCategory.DocumentError,
                $"Field '{name}' is not supported on a paged field within a filter. The paged field resolves to its underlying collection - use it directly (e.g. field.count()) or via items/edges/totalItems/totalCount."
            );
        }

        // we have a schema we follow it for fields etc
        var schemaType = schema.GetSchemaType(context.Type, false, requestContext);

        if (!schemaType.HasField(name, requestContext))
        {
            return MakeConstantFromIdentity(context, schema, name, requestContext);
        }

        if (schemaType.IsEnum)
        {
            return Expression.Constant(Enum.Parse(schemaType.TypeDotnet, name));
        }
        var gqlField = schemaType.GetField(name, requestContext);

        // A paged field's resolve is the Page/Connection wrapper object which only exists for the GraphQL
        // schema - it cannot execute inside a filter (or translate to SQL). Within a filter the field means
        // the underlying collection, so use the pre-paging resolve expression and remember it so a following
        // items/edges/totalItems/totalCount access can be mapped (above). See GitHub issue #378.
        var pagingExt = gqlField.Extensions.FirstOrDefault(e => e is OffsetPagingExtension or ConnectionPagingExtension);
        if (pagingExt != null && gqlField.FieldParam != null)
        {
            var original = pagingExt is OffsetPagingExtension offset ? offset.OriginalFieldExpression : ((ConnectionPagingExtension)pagingExt).OriginalFieldExpression;
            if (original != null)
            {
                var collection = new Util.ParameterReplacer().Replace(original, gqlField.FieldParam, context);
                compileContext.PagedFieldExpressions.Add(collection);
                return collection;
            }
        }

        var isServiceField = gqlField.Services.Count > 0;

        Expression? exp;

        if (isServiceField && compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately)
            // null context so the service expression param is not replaced and we can replace it in the filter extension
            (exp, _) = gqlField.GetExpression(
                gqlField.ResolveExpression!,
                null,
                null,
                null,
                compileContext,
                new Dictionary<string, object?>(),
                null,
                null,
                [],
                false,
                true,
                new Util.ParameterReplacer()
            );
        else
            (exp, _) = gqlField.GetExpression(
                gqlField.ResolveExpression!,
                context,
                null,
                null,
                compileContext,
                new Dictionary<string, object?>(),
                null,
                null,
                [],
                false,
                false,
                new Util.ParameterReplacer()
            );

        // Track service-backed fields for later filter splitting and wrap with a marker only
        // when we are executing in split mode (EF pass + services pass).
        if (isServiceField && exp != null && compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately)
        {
            // Create a unique key and store the extracted fields for this service field on the compile context
            if (gqlField.ExtractedFieldsFromServices != null)
            {
                compileContext.ServiceFieldDependencies.Add(gqlField);
                compileContext.OriginalContext = context;
            }

            var marker = typeof(Util.ServiceExpressionMarker).GetMethod(nameof(Util.ServiceExpressionMarker.MarkService))!.MakeGenericMethod(exp.Type);
            exp = Expression.Call(marker, exp);
        }

        return exp!;
    }

    private static Expression MakeConstantFromIdentity(Expression context, ISchemaProvider? schema, string name, QueryRequestContext requestContext)
    {
        var enumField = schema!.GetEnumTypes().Select(e => e.GetFields().FirstOrDefault(f => f.Name == name)).Where(f => f != null).FirstOrDefault();
        if (enumField != null)
        {
            var constExp = Expression.Constant(Enum.Parse(enumField.ReturnType.TypeDotnet, enumField.Name));
            if (constExp != null)
                return constExp;
        }
        if (schema.HasType(name))
        {
            var type = schema.GetSchemaType(name, requestContext);
            if (type.IsEnum)
            {
                return Expression.Default(type.TypeDotnet);
            }
        }

        throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{name}' not found on type '{schema?.GetSchemaType(context!.Type, false, null)?.Name ?? context!.Type.Name}'");
    }
}

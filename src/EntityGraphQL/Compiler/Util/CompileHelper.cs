using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public static class GraphQLHelper
{
    public static Expression InjectServices(
        IServiceProvider serviceProvider,
        IEnumerable<ParameterExpression> services,
        List<object?> allArgs,
        Expression expression,
        List<ParameterExpression> parameters,
        ParameterReplacer replacer
    )
    {
        foreach (var serviceParam in services)
        {
            // We create a new parameter so each time the expression is used the
            // serviceProvider.GetService is used and the rules registered with the service provider are used
            // e.g. a new instance or a singleton etc.
            var srvParam = Expression.Parameter(serviceParam.Type, $"exec_{serviceParam.Name}");
            parameters.Add(srvParam);
            var service = serviceProvider.GetService(serviceParam.Type) ?? throw new EntityGraphQLExecutionException($"Service {serviceParam.Type.Name} not found in service provider");
            allArgs.Add(service);

            expression = replacer.Replace(expression, serviceParam, srvParam);
        }

        return expression;
    }

    public static Dictionary<string, Expression> ExpressionOnly(this Dictionary<IFieldKey, CompiledField> source)
    {
        return source.ToDictionary(i => i.Key.Name, i => i.Value.Expression);
    }

    public static void ValidateAndReplaceFieldArgs(
        IField field,
        ParameterExpression? argsParam,
        ParameterReplacer replacer,
        ref object? argumentValue,
        ref Expression result,
        List<string> validationErrors,
        ParameterExpression? newArgParam
    )
    {
        // replace the arg param after extensions (don't rely on extensions to do this)
        if (argsParam != null && newArgParam != null && argsParam != newArgParam)
        {
            result = replacer.Replace(result, argsParam, newArgParam);
        }

        if (field.Validators.Count > 0)
        {
            var invokeContext = new ArgumentValidatorContext(field, argumentValue);
            foreach (var m in field.Validators)
            {
                m(invokeContext);
                argumentValue = invokeContext.Arguments;
            }

            validationErrors.AddRange(invokeContext.Errors);
        }

        if (validationErrors.Count > 0)
        {
            throw new EntityGraphQLValidationException(validationErrors);
        }
    }
}

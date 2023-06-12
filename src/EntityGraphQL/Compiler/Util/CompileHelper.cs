using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Compiler
{
    public static class GraphQLHelper
    {
        public static Expression InjectServices(IServiceProvider serviceProvider, IEnumerable<ParameterExpression> services, List<object?> allArgs, Expression expression, List<ParameterExpression> parameters, ParameterReplacer replacer)
        {
            foreach (var serviceParam in services)
            {
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
    }
}
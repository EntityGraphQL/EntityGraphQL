using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Compiler
{
    public static class GraphQLHelper
    {
        public static Expression InjectServices(IServiceProvider? serviceProvider, IEnumerable<Type> services, List<object> allArgs, Expression expression, List<ParameterExpression> parameters, ParameterReplacer replacer)
        {
            foreach (var serviceType in services.Distinct())
            {
                var srvParam = parameters.FirstOrDefault(p => p.Type == serviceType);
                if (srvParam == null)
                {
                    srvParam = Expression.Parameter(serviceType, $"srv_{serviceType.Name}");
                    parameters.Add(srvParam);
                    var service = serviceProvider!.GetService(serviceType);
                    allArgs.Add(service);
                }

                expression = replacer.ReplaceByType(expression, serviceType, srvParam);
            }

            return expression;
        }

        public static Dictionary<string, Expression> ExpressionOnly(this Dictionary<string, CompiledField> source)
        {
            return source.ToDictionary(i => i.Key, i => i.Value.Expression);
        }
    }
}
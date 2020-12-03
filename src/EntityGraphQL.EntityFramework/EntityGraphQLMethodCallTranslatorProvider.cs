using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace EntityGraphQL.EntityFramework.Extensions
{
    /// <summary>
    /// This is where we can collect all our method translators
    /// </summary>
    public class EntityGraphQLMethodCallTranslatorProvider : IMethodCallTranslatorProvider
    {
        public SqlExpression Translate(IModel model, SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments)
        {
            if (method.Name != "WhereWhen")// || method.DeclaringType != typeof(LinqExtensions))
                return null;

            var apply = Expression.Lambda(arguments.Last()).Compile().DynamicInvoke() as bool?;
            if (apply.HasValue && apply.Value == true)
            {
                return arguments.ElementAt(1);
            }
            return arguments.First();
        }
    }
}
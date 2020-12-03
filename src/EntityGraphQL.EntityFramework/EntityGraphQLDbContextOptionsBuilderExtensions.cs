using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityGraphQL.EntityFramework.Extensions
{
    public static class EntityGraphQLDbContextOptionsBuilderExtensions
    {
        private static readonly MethodInfo whereWhenMethod = typeof(LinqExtensions)
            .GetMethod(nameof(LinqExtensions.WhereWhen));

        public static DbContextOptionsBuilder AddEntityGraphQLExtensions(this DbContextOptionsBuilder optionsBuilder)
        {
            var builder = (IDbContextOptionsBuilderInfrastructure)optionsBuilder;

            // if the extension is registered already then we keep it
            // otherwise we create a new one
            var extension = optionsBuilder.Options.FindExtension<EntityGraphQLDbContextOptionsExtension>()
                            ?? new EntityGraphQLDbContextOptionsExtension();
            builder.AddOrUpdateExtension(extension);

            return optionsBuilder;
        }

        public static ModelBuilder AddEntityGraphQLSupport(this ModelBuilder modelBuilder)
        {
            modelBuilder.HasDbFunction(whereWhenMethod)
                        .HasTranslation(expressions =>
                                     {
                                         var apply = Expression.Lambda(expressions.Last()).Compile().DynamicInvoke() as bool?;
                                         if (apply.HasValue && apply.Value == true)
                                         {
                                             return expressions.ElementAt(1);
                                         }
                                         return expressions.First();
                                     });

            return modelBuilder;
        }
    }
}
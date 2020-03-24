using System;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Use this to mark arguments as required when building schemas with arguments.
    /// <code>schemaProvider.AddField("user", new {id = Required<int>()}, (ctx, param) => ctx.Users.Where(u => u.Id == param.id)</code>
    /// </summary>
    public static class ArgumentHelper
    {
        public static RequiredField<TType> Required<TType>()
        {
            return new RequiredField<TType>();
        }

        public static EntityQueryType<TType> EntityQuery<TType>()
        {
            return new EntityQueryType<TType>();
        }

        /// <summary>
        /// Helper to inject services into you GraphQL field selection expressions.
        /// </summary>
        /// <param name="selection"></param>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public static LambdaExpression WithService<TService>(Expression<Func<TService, object>> selection)
        {
            return selection;
        }
        /// <summary>
        /// Helper to inject services into you GraphQL field selection expressions.
        /// </summary>
        /// <param name="selection"></param>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public static LambdaExpression WithService<TService1, TService2>(Expression<Func<TService1, TService2, object>> selection)
        {
            return selection;
        }
        /// <summary>
        /// Helper to inject services into you GraphQL field selection expressions.
        /// </summary>
        /// <param name="selection"></param>
        /// <typeparam name="TService"></typeparam>
        /// <returns></returns>
        public static LambdaExpression WithService<TService1, TService2, TService3>(Expression<Func<TService1, TService2, TService3, object>> selection)
        {
            return selection;
        }
    }

    /// <summary>
    /// Wraps a field/argument, marking it as required when building schemas
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public class RequiredField<TType>
    {
        public Type Type { get; }
        public TType Value { get; set; }

        public RequiredField()
        {
            Type = typeof(TType);
            Value = default(TType);
        }

        public RequiredField(TType value)
        {
            Type = typeof(TType);
            Value = value;
        }

        public static implicit operator TType(RequiredField<TType> field)
        {
            return field.Value;
        }

        public static implicit operator RequiredField<TType>(TType value)
        {
            return new RequiredField<TType>(value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public class EntityQueryType<TType> : BaseEntityQueryType
    {
        /// <summary>
        /// The compiler will end up setting this to the compiled lambda that can be used in LINQ functions
        /// </summary>
        /// <value></value>
        public Expression<Func<TType, bool>> Query { get; set; }
        public override bool HasValue { get => Query != null; }

        public EntityQueryType()
        {
            this.QueryType = typeof(TType);
        }

        public static implicit operator Expression<Func<TType, bool>>(EntityQueryType<TType> q)
        {
            return q.Query;
        }
    }

    public abstract class BaseEntityQueryType
    {
        /// <summary>
        /// Use this in your expression to make a choice
        /// </summary>
        /// <value></value>
        public abstract bool HasValue { get; }
        public Type QueryType { get; protected set; }
    }
}
using System;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Use this to mark arguments as required when building schemas with arguments.
    /// <code>schemaProvider.AddField("user", new {id = Required<int>()}, (ctx, param) => ctx.Users.Where(u => u.Id == param.id)</code>
    /// </summary>
    public static class ArgumentHelper
    {
        /// <summary>
        /// Creates a required argument with the specified type.
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public static RequiredField<TType> Required<TType>()
        {
            return new RequiredField<TType>();
        }

        /// <summary>
        /// Creates a field argument that takes a String value which will be compiled into an expression and used to filter the collection
        /// The argument will not be null if not supplied. Has .HasValue on this argument to test if it have a filter expression.
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public static EntityQueryType<TType> EntityQuery<TType>()
        {
            return new EntityQueryType<TType>();
        }
    }

    /// <summary>
    /// Wraps a field/argument, marking it as required when building schemas
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    public class RequiredField<TType>
    {
        public Type Type { get; }
        public TType? Value { get; set; }

        public RequiredField()
        {
            Type = typeof(TType);
            Value = default;
        }

        public RequiredField(TType value)
        {
            Type = typeof(TType);
            Value = value;
        }

        public static implicit operator TType(RequiredField<TType> field)
        {
            if (field.Value == null)
                throw new EntityGraphQLExecutionException($"Required field argument being used without a value being set. Are you trying to use RequiredField outside a of field expression?");
            return field.Value;
        }

        public static implicit operator RequiredField<TType>(TType value)
        {
            return new RequiredField<TType>(value);
        }

        public override string ToString()
        {
            return Value?.ToString() ?? "null";
        }
    }

    public class EntityQueryType<TType> : BaseEntityQueryType
    {
        /// <summary>
        /// The compiler will end up setting this to the compiled lambda that can be used in LINQ functions
        /// </summary>
        /// <value></value>
        public Expression<Func<TType, bool>>? Query { get; set; }
        public override bool HasValue { get => Query != null; }

        public EntityQueryType() : base(typeof(TType))
        {
        }

        public static implicit operator Expression<Func<TType, bool>>(EntityQueryType<TType> q)
        {
            if (q.Query == null)
                throw new InvalidOperationException("Query is null");
            return q.Query;
        }
    }

    public abstract class BaseEntityQueryType
    {
        public BaseEntityQueryType(Type type)
        {
            QueryType = type;
        }

        /// <summary>
        /// Use this in your expression to make a choice
        /// </summary>
        /// <value></value>
        public abstract bool HasValue { get; }
        public Type QueryType { get; private set; }
    }
}
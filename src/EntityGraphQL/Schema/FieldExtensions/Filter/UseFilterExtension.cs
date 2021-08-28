using System;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseFilterExtension
    {
        /// <summary>
        /// Update field to implement a filter argument that takes an expression (e.g. "field >= 5")
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static Field UseFilter(this Field field)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"UseFilter must only be called on a field that returns an IEnumerable");
            field.AddExtension(new FilterExtension());
            return field;
        }
    }
}
using System;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseOffsetPagingExtension
    {
        /// <summary>
        /// Update field to implement paging with the Connection<> classes and metadata.
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <param name="defaultPageSize">If argument take is null this value will be used</param>
        /// <param name="maxPageSize">If argument take is greater than this value an error will be raised</param>
        /// <returns></returns>
        public static Field UseOffsetPaging(this Field field, int? defaultPageSize = null, int? maxPageSize = null)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"UseOffsetPaging must only be called on a field that returns an IEnumerable");
            field.AddExtension(new OffsetPagingExtension(defaultPageSize, maxPageSize));
            return field;
        }
    }

    public class UseOffsetPagingAttribute : FieldExtensionAttribute
    {
        public int? DefaultPageSize { get; set; }
        public int? MaxPageSize { get; set; }
        public override void ApplyExtension(Field field)
        {
            field.UseOffsetPaging(DefaultPageSize, MaxPageSize);
        }
    }
}
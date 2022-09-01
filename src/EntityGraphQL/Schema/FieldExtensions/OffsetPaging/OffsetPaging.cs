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
        public static IField UseOffsetPaging(this IField field, int? defaultPageSize = null, int? maxPageSize = null)
        {
            field.AddExtension(new OffsetPagingExtension(defaultPageSize, maxPageSize));
            return field;
        }
    }

    public class UseOffsetPagingAttribute : ExtensionAttribute
    {
        public int? DefaultPageSize { get; set; }
        public int? MaxPageSize { get; set; }

        public UseOffsetPagingAttribute() { }
        public UseOffsetPagingAttribute(int defaultPageSize)
        {
            DefaultPageSize = defaultPageSize;
        }

        public UseOffsetPagingAttribute(int defaultPageSize, int maxPageSize)
            : this(defaultPageSize)
        {
            MaxPageSize = maxPageSize;
        }

        public override void ApplyExtension(IField field)
        {
            field.UseOffsetPaging(DefaultPageSize, MaxPageSize);
        }
    }
}
namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseConnectionPagingExtension
    {
        /// <summary>
        /// Update field to implement paging with the Connection<> classes and metadata.
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <param name="defaultPageSize">If no values are passed for first or last arguments. This value will be applied to the first argument</param>
        /// <param name="maxPageSize">If either argument first or last is greater than this value an error is raised</param>
        /// <returns></returns>
        public static IField UseConnectionPaging(this IField field, int? defaultPageSize = null, int? maxPageSize = null)
        {
            field.AddExtension(new ConnectionPagingExtension(defaultPageSize, maxPageSize));
            return field;
        }
    }

    public class UseConnectionPagingAttribute : ExtensionAttribute
    {
        public UseConnectionPagingAttribute()
        {
        }

        public UseConnectionPagingAttribute(int defaultPageSize)
        {
            DefaultPageSize = defaultPageSize;
        }

        public UseConnectionPagingAttribute(int defaultPageSize, int maxPageSize)
            : this(defaultPageSize)
        {
            MaxPageSize = maxPageSize;
        }

        public int? DefaultPageSize { get; set; }
        public int? MaxPageSize { get; set; }
        public override void ApplyExtension(IField field)
        {
            field.UseConnectionPaging(DefaultPageSize, MaxPageSize);
        }
    }
}
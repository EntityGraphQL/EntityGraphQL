
namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseFilterExtension
    {
        /// <summary>
        /// Update a collection field to implement a filter argument that takes an expression string (e.g. "property1 >= 5")
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static Field UseFilter(this Field field)
        {
            field.AddExtension(new FilterExpressionExtension());
            return field;
        }
    }

    public class UseFilterAttribute : FieldExtensionAttribute
    {
        public override void ApplyExtension(Field field)
        {
            field.UseFilter();
        }
    }
}
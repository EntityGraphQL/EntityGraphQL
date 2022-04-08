
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
            field.AddExtension(new FilterExtension());
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
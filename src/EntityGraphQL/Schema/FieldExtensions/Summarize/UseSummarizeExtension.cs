using System;
using System.Collections.Generic;
using System.Text;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseSummarizeExtension
    {
        /// <summary>
        /// Update field to implement a sort argument that takes options to sort a collection
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static Field UseSummarize(this Field field)
        {
            field.AddExtension(new SummarizeExtension());
            return field;
        }
    }

    public class UseSummarizeAttribute : FieldExtensionAttribute
    {
        public UseSummarizeAttribute() { }

        public override void ApplyExtension(Field field)
        {
            field.UseSummarize();
        }
    }
}

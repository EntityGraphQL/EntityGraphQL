using System;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseSortExtension
    {
        /// <summary>
        /// Update field to implement a sort argument that takes options to sort a collection
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <param name="fieldSelection">Select the fields you want available for sorting.
        /// T must be the context of the collection you are applying the sort to</param>
        /// <returns></returns>
        public static IField UseSort<ElementType, ReturnType>(this IField field, Expression<Func<ElementType, ReturnType>> fieldSelection)
        {
            field.AddExtension(new SortExtension(fieldSelection.ReturnType, null, null));
            return field;
        }
        /// <summary>
        /// Update field to implement a sort argument that takes options to sort a collection
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <param name="fieldSelection">Select the fields you want available for sorting.
        /// T must be the context of the collection you are applying the sort to</param>
        /// <param name="defaultSort">Sort to use if no sort argument supplied in query</param>
        /// <param name="direction">Direction of the default sort</param>
        /// <returns></returns>
        public static IField UseSort<ElementType, ReturnType, TSort>(this IField field, Expression<Func<ElementType, ReturnType>> fieldSelection, Expression<Func<ElementType, TSort>> defaultSort, SortDirectionEnum direction)
        {
            field.AddExtension(new SortExtension(fieldSelection.ReturnType, defaultSort, direction));
            return field;
        }

        /// <summary>
        /// Update field to implement a sort argument that takes options to sort a collection
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <param name="defaultSort">Sort to use if no sort argument supplied in query</param>
        /// <param name="direction">Direction of the default sort</param>
        /// <returns></returns>
        public static IField UseSort<ElementType, TSort>(this IField field, Expression<Func<ElementType, TSort>> defaultSort, SortDirectionEnum direction)
        {
            field.AddExtension(new SortExtension(null, defaultSort, direction));
            return field;
        }

        /// <summary>
        /// Update field to implement a sort argument that takes options to sort a collection
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static IField UseSort(this IField field)
        {
            field.AddExtension(new SortExtension(null, null, null));
            return field;
        }
    }

    public class UseSortAttribute : ExtensionAttribute
    {
        public UseSortAttribute() { }

        public override void ApplyExtension(IField field)
        {
            field.UseSort();
        }
    }
}
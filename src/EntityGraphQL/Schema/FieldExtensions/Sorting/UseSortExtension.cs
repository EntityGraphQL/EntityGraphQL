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
        public static IField UseSort<TElementType, TReturnType>(this IField field, Expression<Func<TElementType, TReturnType>> fieldSelection)
        {
            field.AddExtension(new SortExtension(fieldSelection, false));
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
        public static IField UseSort<TElementType, TReturnType>(
            this IField field,
            Expression<Func<TElementType, TReturnType>> fieldSelection,
            Expression<Func<TElementType, object>> defaultSort,
            SortDirection direction
        )
        {
            field.AddExtension(new SortExtension(fieldSelection, false, new Sort<TElementType>(defaultSort, direction)));
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
        public static IField UseSort<TElementType>(this IField field, Expression<Func<TElementType, object>> defaultSort, SortDirection direction)
        {
            field.AddExtension(new SortExtension(null, false, new Sort<TElementType>(defaultSort, direction)));
            return field;
        }

        public static IField UseSort<TElementType, TReturnType>(this IField field, Expression<Func<TElementType, TReturnType>> fieldSelection, params Sort<TElementType>[] defaultSorts)
        {
            field.AddExtension(new SortExtension(fieldSelection, false, defaultSorts));
            return field;
        }

        public static IField UseSort<TElementType>(this IField field, params Sort<TElementType>[] defaultSorts)
        {
            field.AddExtension(new SortExtension(null, false, defaultSorts));
            return field;
        }

        public static IField UseSort<TElementType>(this IField field, bool useSchemaFields, params Sort<TElementType>[] defaultSorts)
        {
            field.AddExtension(new SortExtension(null, useSchemaFields, defaultSorts));
            return field;
        }

        /// <summary>
        /// Update field to implement a sort argument that takes options to sort a collection
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <param name="useSchemaFields">Use the schema fields for sorting. Will use the fields current in the schema only</param>
        /// <returns></returns>
        public static IField UseSort(this IField field, bool useSchemaFields = false)
        {
            field.AddExtension(new SortExtension(null, useSchemaFields));
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

    public class Sort<TElementType> : ISort
    {
        public LambdaExpression SortExpression { get; set; }
        public SortDirection Direction { get; set; }

        public Sort(Expression<Func<TElementType, object>> sortExpression)
        {
            SortExpression = sortExpression;
            Direction = SortDirection.ASC;
        }

        public Sort(Expression<Func<TElementType, object>> sortExpression, SortDirection direction)
        {
            SortExpression = sortExpression;
            Direction = direction;
        }
    }

    public interface ISort
    {
        LambdaExpression SortExpression { get; set; }
        SortDirection Direction { get; set; }
    }
}

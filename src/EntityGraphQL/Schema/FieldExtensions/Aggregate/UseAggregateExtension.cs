using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema.FieldExtensions;

public static class UseAggregateExtension
{
    /// <summary>
    /// If the field is a list, add a new field at the same level with the name {field}Aggregate
    /// Only call on a field that returns an IEnumerable
    /// </summary>
    /// <param name="field"></param>
    /// <param name="fieldName">Use this for the name of the created field. Is null the field will be called <field-name>Aggregate</param>
    /// <returns></returns>
    public static IField UseAggregate(this IField field, string? fieldName = null)
    {
        return field.AddExtension(new AggregateExtension(fieldName, null, false));
    }

    /// <summary>
    /// If the field is a list, add a new field at the same level with the name {field}Aggregate
    /// Only call on a field that returns an IEnumerable
    /// </summary>
    /// <typeparam name="TElementType"></typeparam>
    /// <typeparam name="TReturnType"></typeparam>
    /// <typeparam name="TSort"></typeparam>
    /// <param name="field"></param>
    /// <param name="fieldSelection">
    /// GraphQL Schema field names to include or exclude in building the aggregation fields. By default numerical and date fields 
    /// will have the relevant aggregation fields created. If the field is not available on the type it will be ignored. 
    /// Field name in GraphQL Schema are case sensitive.
    /// </param>
    /// <param name="excludeFields">If true, the fields in fieldSelection will be excluded from the aggregate fields instead</param>
    /// <param name="fieldName"></param>
    /// <returns></returns>
    public static IField UseAggregate(this IField field, IEnumerable<string>? fieldSelection, bool excludeFields = false, string? fieldName = null)
    {
        return field.AddExtension(new AggregateExtension(fieldName, fieldSelection, excludeFields));
    }
}

public class UseAggregateAttribute : ExtensionAttribute
{
    /// <summary>
    /// Overrides the default name of the aggregate field. If null the field will be called <field-name>Aggregate
    /// </summary>
    public string? FieldName { get; set; }
    /// <summary>
    /// If false, you will need to use [IncludeAggregateField] to include fields in the aggregate
    /// </summary>
    public bool AutoAddFields { get; set; } = true;

    public UseAggregateAttribute() { }

    public override void ApplyExtension(IField field)
    {
        var fieldList = AutoAddFields ? FindExcludedFields(field) : FindIncludedFields(field);
        field.UseAggregate(fieldList, AutoAddFields, FieldName);
    }

    private static IEnumerable<string> FindIncludedFields(IField field)
    {
        var includedFields = new List<string>();
        foreach (var prop in field.ReturnType.SchemaType.TypeDotnet.GetProperties())
        {
            if (prop.GetCustomAttributes(typeof(IncludeAggregateFieldAttribute), true).Length > 0)
            {
                var (name, _) = SchemaBuilder.GetNameAndDescription(prop, field.Schema);
                includedFields.Add(name);
            }
        }
        return includedFields;
    }

    private static IEnumerable<string> FindExcludedFields(IField field)
    {
        var excludedFields = new List<string>();
        foreach (var prop in field.ReturnType.SchemaType.TypeDotnet.GetProperties())
        {
            if (prop.GetCustomAttributes(typeof(ExcludeAggregateFieldAttribute), true).Length > 0)
            {
                var (name, _) = SchemaBuilder.GetNameAndDescription(prop, field.Schema);
                excludedFields.Add(name);
            }
        }
        return excludedFields;
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false)]
public class IncludeAggregateFieldAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false)]
public class ExcludeAggregateFieldAttribute : Attribute
{
}
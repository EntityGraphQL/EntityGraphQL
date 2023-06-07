using System.Collections.Generic;
using System.Reflection;

namespace EntityGraphQL.Schema;
public class ArgumentValidatorContext
{
    private readonly List<string> errors = new();
    public ArgumentValidatorContext(IField field, object? argumentValues, MethodInfo? method = null)
    {
        Field = field;
        Arguments = argumentValues;
        Method = method;
    }

    /// <summary>
    /// The value of the argments for the field.
    /// </summary>
    public dynamic? Arguments { get; set; }

    /// <summary>
    /// The method (mutation) about to be called
    /// </summary>
    public MethodInfo? Method { get; }

    /// <summary>
    /// Information about the field as defined in the schema.
    /// </summary>
    public IField Field { get; }
    /// <summary>
    /// List of error messages that will be added to the result
    /// </summary>
    public IReadOnlyList<string> Errors { get => errors; }

    /// <summary>
    /// Add an error message to the result. This will prevent execution of the query
    /// </summary>
    /// <param name="error"></param>
    public void AddError(string error)
    {
        errors.Add($"Field '{Field.Name}' - {error}");
    }
}
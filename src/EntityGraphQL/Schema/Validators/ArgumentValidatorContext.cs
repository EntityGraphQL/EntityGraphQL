using System.Collections.Generic;

namespace EntityGraphQL.Schema;
public class ArgumentValidatorContext
{
    private readonly List<string> errors = new();
    public ArgumentValidatorContext(IField field, object? argumentValues)
    {
        Field = field;
        Arguments = argumentValues;
    }

    /// <summary>
    /// The value of the argments for the field.
    /// </summary>
    public object? Arguments { get; set; }
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
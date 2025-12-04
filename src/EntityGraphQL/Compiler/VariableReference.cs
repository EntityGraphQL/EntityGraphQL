namespace EntityGraphQL.Compiler;

/// <summary>
/// Represents an unresolved variable reference in a fragment definition.
/// Variables in fragments cannot be resolved until the fragment is used within an operation,
/// since the fragment doesn't have access to the operation's variable context during parsing.
/// </summary>
internal sealed class VariableReference
{
    public string VariableName { get; }

    public VariableReference(string variableName)
    {
        VariableName = variableName;
    }
}

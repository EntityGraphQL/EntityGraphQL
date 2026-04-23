using System;

namespace EntityGraphQL;

/// <summary>
/// Represents a source location in a GraphQL document.
/// </summary>
public sealed class GraphQLSourceLocation : IEquatable<GraphQLSourceLocation>
{
    public int Position { get; }
    public int Line { get; }
    public int Column { get; }

    public GraphQLSourceLocation(int position, int line, int column)
    {
        Position = position;
        Line = line;
        Column = column;
    }

    public bool Equals(GraphQLSourceLocation? other)
    {
        if (other == null)
            return false;

        return Position == other.Position && Line == other.Line && Column == other.Column;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as GraphQLSourceLocation);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Position, Line, Column);
    }
}

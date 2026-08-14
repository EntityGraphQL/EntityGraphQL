using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

internal sealed class SelectedField(string name, IField? schemaField) : ISelectedField
{
    public string Name { get; } = name;
    public IField? SchemaField { get; } = schemaField;
    public List<SelectedField> Children { get; } = [];
    public IReadOnlyList<ISelectedField> Fields => Children;
}

/// <summary>
/// The fields the engine will read off the objects a resolver returns. Built from the field's selection, with
/// service fields replaced by the members their resolvers read - see <see cref="IFieldSelection"/> for what
/// that means for a caller.
/// </summary>
internal sealed class FieldSelection : IFieldSelection
{
    private readonly List<SelectedField> fields = [];
    private List<string>? paths;

    public IReadOnlyList<ISelectedField> Fields => fields;

    public IReadOnlyList<string> Paths => paths ??= [.. Flatten(fields, null)];

    public bool Contains(string path) => Paths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Flatten(IEnumerable<SelectedField> fields, string? prefix)
    {
        foreach (var field in fields)
        {
            var path = prefix == null ? field.Name : $"{prefix}.{field.Name}";
            if (field.Children.Count == 0)
                yield return path;
            else
            {
                foreach (var child in Flatten(field.Children, path))
                    yield return child;
            }
        }
    }

    /// <summary>
    /// Read the members <paramref name="expression"/> takes off <paramref name="rootParam"/> into the
    /// selection - e.g. a bulk resolver's key selector, or the expression of a service field we cannot load
    /// ourselves, whose members we have to return so the engine can resolve it afterwards.
    /// </summary>
    public void AddMembersOf(Expression expression, ParameterExpression rootParam)
    {
        foreach (var path in MemberPathCollector.Collect(expression, rootParam))
            AddPath(path, null);
    }

    public void AddPath(IReadOnlyList<string> path, IField? schemaField)
    {
        var into = fields;
        for (var i = 0; i < path.Count; i++)
        {
            var isLast = i == path.Count - 1;
            var existing = into.FirstOrDefault(f => f.Name == path[i]);
            if (existing == null)
            {
                existing = new SelectedField(path[i], isLast ? schemaField : null);
                into.Add(existing);
            }
            // a field read as a whole elsewhere stays read as a whole - do not narrow it to some of its members
            if (isLast || existing.Children.Count == 0 && i > 0 && existing.SchemaField != null)
                return;
            into = existing.Children;
        }
        paths = null;
    }

    public SelectedField AddField(string name, IField? schemaField)
    {
        var existing = fields.FirstOrDefault(f => f.Name == name);
        if (existing != null)
            return existing;
        var field = new SelectedField(name, schemaField);
        fields.Add(field);
        paths = null;
        return field;
    }
}

/// <summary>Collects the member paths an expression reads off a parameter - x.Owner.Name gives [Owner, Name].</summary>
internal sealed class MemberPathCollector(ParameterExpression rootParam) : ExpressionVisitor
{
    private readonly List<IReadOnlyList<string>> paths = [];

    public static IReadOnlyList<IReadOnlyList<string>> Collect(Expression expression, ParameterExpression rootParam)
    {
        var collector = new MemberPathCollector(rootParam);
        collector.Visit(expression);
        return collector.paths;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        var path = new List<string>();
        Expression? current = node;
        while (current is MemberExpression member)
        {
            path.Insert(0, member.Member.Name);
            current = member.Expression;
        }
        if (current == rootParam && path.Count > 0)
        {
            paths.Add(path);
            return node;
        }
        return base.VisitMember(node);
    }
}

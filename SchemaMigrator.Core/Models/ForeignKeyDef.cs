namespace SchemaMigrator.Core.Models;

/// <summary>
/// Represents a foreign key constraint definition.
/// </summary>
public sealed class ForeignKeyDef
{
    public required string Name { get; init; }
    public required string Table { get; init; }
    public required IReadOnlyList<string> Columns { get; init; }
    public required string RefTable { get; init; }
    public required IReadOnlyList<string> RefColumns { get; init; }
    public required string OnDelete { get; init; }
    public required string OnUpdate { get; init; }

    /// <summary>
    /// Whether the FK references itself (self-referencing FK).
    /// </summary>
    public bool IsSelfReferencing =>
        Table.Equals(RefTable, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Standard foreign key referential actions.
/// </summary>
public static class ForeignKeyAction
{
    public const string Cascade = "CASCADE";
    public const string SetNull = "SET NULL";
    public const string SetDefault = "SET DEFAULT";
    public const string Restrict = "RESTRICT";
    public const string NoAction = "NO ACTION";
}

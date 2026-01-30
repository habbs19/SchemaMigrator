namespace SchemaMigrator.Core.Models;

/// <summary>
/// Represents a database index definition.
/// </summary>
public sealed class IndexDef
{
    public required string Name { get; init; }

    /// <summary>
    /// Whether this is a unique index.
    /// </summary>
    public bool IsUnique { get; init; }

    /// <summary>
    /// Index type (BTREE, HASH, FULLTEXT, SPATIAL).
    /// </summary>
    public required string IndexType { get; init; }

    /// <summary>
    /// Columns included in the index, in order.
    /// </summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>
    /// Column sort directions (ASC/DESC) for each column.
    /// </summary>
    public IReadOnlyList<string> ColumnDirections { get; init; } = [];

    /// <summary>
    /// Prefix lengths for each column (null if no prefix).
    /// </summary>
    public IReadOnlyList<int?> PrefixLengths { get; init; } = [];

    /// <summary>
    /// Whether this is a fulltext index.
    /// </summary>
    public bool IsFullText => IndexType.Equals("FULLTEXT", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this is a spatial index.
    /// </summary>
    public bool IsSpatial => IndexType.Equals("SPATIAL", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Index comment.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Whether the index is visible (MySQL 8+).
    /// </summary>
    public bool IsVisible { get; init; } = true;
}

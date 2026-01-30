namespace SchemaMigrator.Core.Models;

/// <summary>
/// Represents a database table definition.
/// </summary>
public sealed class TableDef
{
    public required string Name { get; init; }

    /// <summary>
    /// Storage engine (e.g., InnoDB, MyISAM).
    /// </summary>
    public string? Engine { get; init; }

    /// <summary>
    /// Default character set for the table.
    /// </summary>
    public string? Charset { get; init; }

    /// <summary>
    /// Default collation for the table.
    /// </summary>
    public string? Collation { get; init; }

    /// <summary>
    /// Table comment.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Auto-increment starting value.
    /// </summary>
    public long? AutoIncrement { get; init; }

    public Dictionary<string, ColumnDef> Columns { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IndexDef> Indexes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ForeignKeyDef> ForeignKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IndexDef? PrimaryKey { get; set; }

    /// <summary>
    /// Gets columns in ordinal order.
    /// </summary>
    public IEnumerable<ColumnDef> ColumnsOrdered =>
        Columns.Values.OrderBy(c => c.OrdinalPosition);
}

namespace SchemaMigrator.Core.Models;

/// <summary>
/// Represents a database column definition.
/// </summary>
public sealed class ColumnDef
{
    public required string Name { get; init; }

    /// <summary>
    /// Full column type including size/precision (e.g., "bigint unsigned", "varchar(100)").
    /// </summary>
    public required string ColumnType { get; init; }

    /// <summary>
    /// Whether the column allows NULL values.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Default value (normalized).
    /// </summary>
    public string? Default { get; init; }

    /// <summary>
    /// Extra attributes (auto_increment, on update current_timestamp, etc).
    /// </summary>
    public string Extra { get; init; } = "";

    /// <summary>
    /// Character set for string columns.
    /// </summary>
    public string? Charset { get; init; }

    /// <summary>
    /// Collation for string columns.
    /// </summary>
    public string? Collation { get; init; }

    /// <summary>
    /// Column comment.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Ordinal position in the table (1-based).
    /// </summary>
    public int OrdinalPosition { get; init; }

    /// <summary>
    /// Whether this column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Whether this column has auto_increment.
    /// </summary>
    public bool IsAutoIncrement =>
        Extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this column is a generated/computed column.
    /// </summary>
    public bool IsGenerated =>
        Extra.Contains("GENERATED", StringComparison.OrdinalIgnoreCase) ||
        Extra.Contains("VIRTUAL", StringComparison.OrdinalIgnoreCase) ||
        Extra.Contains("STORED", StringComparison.OrdinalIgnoreCase);
}

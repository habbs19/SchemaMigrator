namespace SchemaMigrator.Core.Models;

/// <summary>
/// Represents a complete snapshot of a database schema at a point in time.
/// </summary>
public sealed class SchemaSnapshot
{
    /// <summary>
    /// Tables in this schema, keyed by table name (case-insensitive).
    /// </summary>
    public Dictionary<string, TableDef> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Schema name (database name).
    /// </summary>
    public string? SchemaName { get; init; }

    /// <summary>
    /// Default character set for the schema.
    /// </summary>
    public string? DefaultCharset { get; init; }

    /// <summary>
    /// Default collation for the schema.
    /// </summary>
    public string? DefaultCollation { get; init; }

    /// <summary>
    /// Timestamp when this snapshot was taken.
    /// </summary>
    public DateTimeOffset? CapturedAt { get; init; }

    /// <summary>
    /// Database server version.
    /// </summary>
    public string? ServerVersion { get; init; }

    /// <summary>
    /// Gets all table names.
    /// </summary>
    public IEnumerable<string> TableNames => Tables.Keys;

    /// <summary>
    /// Gets total column count across all tables.
    /// </summary>
    public int TotalColumns => Tables.Values.Sum(t => t.Columns.Count);

    /// <summary>
    /// Gets total index count across all tables.
    /// </summary>
    public int TotalIndexes => Tables.Values.Sum(t => t.Indexes.Count);

    /// <summary>
    /// Gets total foreign key count across all tables.
    /// </summary>
    public int TotalForeignKeys => Tables.Values.Sum(t => t.ForeignKeys.Count);
}

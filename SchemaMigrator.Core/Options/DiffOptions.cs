namespace SchemaMigrator.Core.Options;

/// <summary>
/// Options controlling how schema differences are calculated.
/// </summary>
public sealed class DiffOptions
{
    /// <summary>
    /// Allow destructive changes (DROP TABLE, DROP COLUMN, DROP INDEX, DROP FK).
    /// If false, destructive operations are still detected but must be blocked later.
    /// </summary>
    public bool AllowDestructive { get; init; } = false;

    /// <summary>
    /// Ignore index differences entirely.
    /// Useful for environments where indexes are managed separately.
    /// </summary>
    public bool IgnoreIndexes { get; init; } = false;

    /// <summary>
    /// Ignore foreign key differences entirely.
    /// Useful for phased rollouts or legacy schemas.
    /// </summary>
    public bool IgnoreForeignKeys { get; init; } = false;

    /// <summary>
    /// Ignore primary key differences.
    /// </summary>
    public bool IgnorePrimaryKeys { get; init; } = false;

    /// <summary>
    /// Treat column type changes as destructive instead of risky.
    /// Conservative mode for production safety.
    /// </summary>
    public bool StrictColumnTypeChanges { get; init; } = false;

    /// <summary>
    /// Ignore column default value changes.
    /// </summary>
    public bool IgnoreDefaults { get; init; } = false;

    /// <summary>
    /// Ignore charset and collation differences.
    /// </summary>
    public bool IgnoreCharsetCollation { get; init; } = false;

    /// <summary>
    /// Ignore column EXTRA attributes (auto_increment, on update, etc).
    /// </summary>
    public bool IgnoreExtra { get; init; } = false;

    /// <summary>
    /// Tables to exclude from comparison (case-insensitive).
    /// Useful for excluding migration history tables, temp tables, etc.
    /// </summary>
    public IReadOnlySet<string> ExcludeTables { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Table name patterns to exclude (supports * wildcard).
    /// Example: "temp_*", "*_backup"
    /// </summary>
    public IReadOnlyList<string> ExcludeTablePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates options for a safe, conservative comparison.
    /// </summary>
    public static DiffOptions SafeMode => new()
    {
        AllowDestructive = false,
        StrictColumnTypeChanges = true
    };

    /// <summary>
    /// Creates options that allow all changes.
    /// </summary>
    public static DiffOptions FullSync => new()
    {
        AllowDestructive = true
    };
}

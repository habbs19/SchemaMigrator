namespace SchemaMigrator.Core.Options;

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
    /// Treat column type changes as destructive instead of risky.
    /// Conservative mode for production safety.
    /// </summary>
    public bool StrictColumnTypeChanges { get; init; } = false;
}
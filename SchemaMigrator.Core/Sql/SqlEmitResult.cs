using SchemaMigrator.Core.Diff;

namespace SchemaMigrator.Core.Sql;

/// <summary>
/// Result of SQL emission containing all generated SQL statements.
/// </summary>
public sealed class SqlEmitResult
{
    /// <summary>
    /// SQL statements that must be reviewed and/or executed
    /// before applying the migration (e.g., FK orphan checks,
    /// NOT NULL validation, truncation checks).
    /// </summary>
    public IReadOnlyList<string> PrecheckSql { get; init; } = Array.Empty<string>();

    /// <summary>
    /// SQL statements that perform the actual schema migration.
    /// These are emitted in correct, safe execution order.
    /// </summary>
    public IReadOnlyList<string> MigrationSql { get; init; } = Array.Empty<string>();

    /// <summary>
    /// SQL statements to rollback the migration.
    /// Only populated when EmitOptions.GenerateRollback is true.
    /// </summary>
    public IReadOnlyList<string> RollbackSql { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Warnings encountered during SQL generation.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether all operations could be emitted successfully.
    /// </summary>
    public bool IsComplete { get; init; } = true;

    /// <summary>
    /// Operations that could not be emitted (unsupported).
    /// </summary>
    public IReadOnlyList<DiffOperation> SkippedOperations { get; init; } = Array.Empty<DiffOperation>();
}

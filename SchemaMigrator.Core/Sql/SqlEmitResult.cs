namespace SchemaMigrator.Core.Sql;

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
}

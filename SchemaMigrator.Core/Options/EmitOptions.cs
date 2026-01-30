namespace SchemaMigrator.Core.Options;

/// <summary>
/// Options controlling SQL emission.
/// </summary>
public sealed class EmitOptions
{
    /// <summary>
    /// Allow emitting destructive SQL (DROP TABLE, DROP COLUMN, etc).
    /// </summary>
    public bool AllowDestructive { get; init; } = false;

    /// <summary>
    /// Wrap migration in a transaction.
    /// Note: DDL in MySQL causes implicit commit, so this provides limited protection.
    /// </summary>
    public bool UseTransaction { get; init; } = true;

    /// <summary>
    /// Generate rollback SQL alongside migration SQL.
    /// </summary>
    public bool GenerateRollback { get; init; } = false;

    /// <summary>
    /// Include comments describing each operation.
    /// </summary>
    public bool IncludeComments { get; init; } = true;

    /// <summary>
    /// Include timestamp in generated SQL header.
    /// </summary>
    public bool IncludeTimestamp { get; init; } = true;

    /// <summary>
    /// Schema name to use in generated SQL (null = use default/current schema).
    /// </summary>
    public string? SchemaName { get; init; }

    /// <summary>
    /// Add IF EXISTS / IF NOT EXISTS clauses where supported.
    /// </summary>
    public bool UseConditionalStatements { get; init; } = false;

    /// <summary>
    /// Disable foreign key checks during migration.
    /// Useful for complex migrations with circular references.
    /// </summary>
    public bool DisableForeignKeyChecks { get; init; } = false;

    /// <summary>
    /// Lock timeout in seconds for DDL operations.
    /// </summary>
    public int? LockWaitTimeout { get; init; }

    /// <summary>
    /// Use ALGORITHM=INPLACE and LOCK=NONE where possible (MySQL 8+).
    /// </summary>
    public bool PreferOnlineDdl { get; init; } = false;
}

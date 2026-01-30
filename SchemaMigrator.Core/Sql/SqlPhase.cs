namespace SchemaMigrator.Core.Sql;

/// <summary>
/// SQL execution phases in safe dependency order.
/// Operations are emitted in ascending phase order.
/// </summary>
public enum SqlPhase
{
    /// <summary>Phase 0: Preparation (disable FK checks, set variables).</summary>
    Preparation = 0,

    /// <summary>Phase 1: Create new tables (no columns yet if complex).</summary>
    CreateTables = 1,

    /// <summary>Phase 2: Add new columns to existing tables.</summary>
    AddColumns = 2,

    /// <summary>Phase 3: Backfill data (populate new columns, migrate data).</summary>
    Backfill = 3,

    /// <summary>Phase 4: Add new indexes.</summary>
    AddIndexes = 4,

    /// <summary>Phase 5: Add foreign key constraints.</summary>
    AddForeignKeys = 5,

    /// <summary>Phase 6: Modify existing columns (type changes, nullability).</summary>
    ModifyColumns = 6,

    /// <summary>Phase 7: Drop constraints, indexes, columns, tables.</summary>
    Drops = 7,

    /// <summary>Phase 8: Cleanup (re-enable FK checks, etc).</summary>
    Cleanup = 8
}

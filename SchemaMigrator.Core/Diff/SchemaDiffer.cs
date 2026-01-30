using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;
using SchemaMigrator.Core.Sql;
using System.Text.RegularExpressions;

namespace SchemaMigrator.Core.Diff;

/// <summary>
/// Compares two schema snapshots and produces a migration plan.
/// </summary>
public sealed class SchemaDiffer
{
    /// <summary>
    /// Compares source (current) schema against target (desired) schema.
    /// </summary>
    /// <param name="source">Current schema (e.g., production).</param>
    /// <param name="target">Desired schema (e.g., development).</param>
    /// <param name="options">Comparison options.</param>
    /// <returns>A diff plan containing all required operations.</returns>
    public DiffPlan Diff(
        SchemaSnapshot source,
        SchemaSnapshot target,
        DiffOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        var plan = new DiffPlan();

        // Filter tables based on exclusion options
        var sourceTables = FilterTables(source.Tables, options);
        var targetTables = FilterTables(target.Tables, options);

        // Order matters for dependencies
        DiffTables(sourceTables, targetTables, plan, options);
        DiffPrimaryKeys(sourceTables, targetTables, plan, options);
        DiffColumns(sourceTables, targetTables, plan, options);
        DiffIndexes(sourceTables, targetTables, plan, options);
        DiffForeignKeys(sourceTables, targetTables, plan, options);

        return plan;
    }

    private static Dictionary<string, TableDef> FilterTables(
        Dictionary<string, TableDef> tables,
        DiffOptions options)
    {
        return tables
            .Where(kvp => !IsTableExcluded(kvp.Key, options))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTableExcluded(string tableName, DiffOptions options)
    {
        if (options.ExcludeTables.Contains(tableName))
            return true;

        foreach (var pattern in options.ExcludeTablePatterns)
        {
            if (MatchesPattern(tableName, pattern))
                return true;
        }

        return false;
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
    }

    // ========================================
    // TABLE DIFFING
    // ========================================

    private static void DiffTables(
        Dictionary<string, TableDef> source,
        Dictionary<string, TableDef> target,
        DiffPlan plan,
        DiffOptions options)
    {
        // 1. Added tables (in target but not in source) - SAFE
        foreach (var (tableName, targetTable) in target)
        {
            if (!source.ContainsKey(tableName))
            {
                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.CreateTables,
                    OperationType: DiffOperationType.CreateTable,
                    TableName: tableName,
                    ObjectName: null,
                    Description: $"Create table {tableName}",
                    IsRisky: false,
                    IsDestructive: false
                ));
            }
        }

        // 2. Removed tables (in source but not in target) - DESTRUCTIVE
        foreach (var (tableName, sourceTable) in source)
        {
            if (!target.ContainsKey(tableName))
            {
                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.Drops,
                    OperationType: DiffOperationType.DropTable,
                    TableName: tableName,
                    ObjectName: null,
                    Description: $"Drop table {tableName}",
                    IsRisky: false,
                    IsDestructive: true
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.DestructiveChange,
                    Table = tableName,
                    Message = "Table will be dropped — ALL data will be permanently lost.",
                    Mitigation = "Backup the table data before migration or rename instead of drop."
                });
            }
        }
    }

    // ========================================
    // PRIMARY KEY DIFFING
    // ========================================

    private static void DiffPrimaryKeys(
        Dictionary<string, TableDef> source,
        Dictionary<string, TableDef> target,
        DiffPlan plan,
        DiffOptions options)
    {
        if (options.IgnorePrimaryKeys)
            return;

        foreach (var (tableName, sourceTable) in source)
        {
            if (!target.TryGetValue(tableName, out var targetTable))
                continue;

            var sourcePk = sourceTable.PrimaryKey;
            var targetPk = targetTable.PrimaryKey;

            // Both null - no PK, nothing to do
            if (sourcePk == null && targetPk == null)
                continue;

            // Added PK
            if (sourcePk == null && targetPk != null)
            {
                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.AddIndexes,
                    OperationType: DiffOperationType.AddPrimaryKey,
                    TableName: tableName,
                    ObjectName: "PRIMARY",
                    Description: $"Add primary key to {tableName}",
                    IsRisky: true,
                    IsDestructive: false
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.RiskyChange,
                    Table = tableName,
                    ObjectName = "PRIMARY KEY",
                    Message = "Adding primary key may fail if duplicate/null values exist.",
                    Mitigation = "Verify data uniqueness and non-null values before migration."
                });
                continue;
            }

            // Removed PK
            if (sourcePk != null && targetPk == null)
            {
                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.Drops,
                    OperationType: DiffOperationType.DropPrimaryKey,
                    TableName: tableName,
                    ObjectName: "PRIMARY",
                    Description: $"Drop primary key from {tableName}",
                    IsRisky: true,
                    IsDestructive: true
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.DestructiveChange,
                    Table = tableName,
                    ObjectName = "PRIMARY KEY",
                    Message = "Dropping primary key removes uniqueness constraint."
                });
                continue;
            }

            // Modified PK
            if (!PrimaryKeysEqual(sourcePk!, targetPk!))
            {
                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.Drops,
                    OperationType: DiffOperationType.DropPrimaryKey,
                    TableName: tableName,
                    ObjectName: "PRIMARY",
                    Description: $"Drop primary key from {tableName} (columns changed)",
                    IsRisky: true,
                    IsDestructive: true
                ));

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.AddIndexes,
                    OperationType: DiffOperationType.AddPrimaryKey,
                    TableName: tableName,
                    ObjectName: "PRIMARY",
                    Description: $"Add primary key to {tableName} (columns changed)",
                    IsRisky: true,
                    IsDestructive: false
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.RiskyChange,
                    Table = tableName,
                    ObjectName = "PRIMARY KEY",
                    Message = $"Primary key columns changing: ({string.Join(", ", sourcePk!.Columns)}) → ({string.Join(", ", targetPk!.Columns)})",
                    Mitigation = "Verify new PK columns have unique, non-null values."
                });
            }
        }
    }

    private static bool PrimaryKeysEqual(IndexDef a, IndexDef b)
    {
        if (a.Columns.Count != b.Columns.Count)
            return false;

        for (int i = 0; i < a.Columns.Count; i++)
        {
            if (!string.Equals(a.Columns[i], b.Columns[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    // ========================================
    // COLUMN DIFFING
    // ========================================

    private static void DiffColumns(
        Dictionary<string, TableDef> source,
        Dictionary<string, TableDef> target,
        DiffPlan plan,
        DiffOptions options)
    {
        foreach (var (tableName, sourceTable) in source)
        {
            if (!target.TryGetValue(tableName, out var targetTable))
                continue; // Table drop handled elsewhere

            var sourceColumns = sourceTable.Columns;
            var targetColumns = targetTable.Columns;

            // 1. Added columns - SAFE (usually)
            foreach (var (colName, targetCol) in targetColumns)
            {
                if (!sourceColumns.ContainsKey(colName))
                {
                    var isRisky = !targetCol.IsNullable && targetCol.Default == null;

                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.AddColumns,
                        OperationType: DiffOperationType.AddColumn,
                        TableName: tableName,
                        ObjectName: colName,
                        Description: $"Add column {tableName}.{colName}",
                        IsRisky: isRisky,
                        IsDestructive: false
                    ));

                    if (isRisky)
                    {
                        plan.AddWarning(new DiffWarning
                        {
                            Type = DiffWarningType.RiskyChange,
                            Table = tableName,
                            ObjectName = colName,
                            Message = "Adding NOT NULL column without DEFAULT may fail if table has existing rows.",
                            Mitigation = "Add column as NULL first, backfill data, then alter to NOT NULL."
                        });
                    }
                }
            }

            // 2. Removed columns - DESTRUCTIVE
            foreach (var (colName, sourceCol) in sourceColumns)
            {
                if (!targetColumns.ContainsKey(colName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.Drops,
                        OperationType: DiffOperationType.DropColumn,
                        TableName: tableName,
                        ObjectName: colName,
                        Description: $"Drop column {tableName}.{colName}",
                        IsRisky: false,
                        IsDestructive: true
                    ));

                    plan.AddWarning(new DiffWarning
                    {
                        Type = DiffWarningType.DestructiveChange,
                        Table = tableName,
                        ObjectName = colName,
                        Message = "Column will be dropped — data will be permanently lost.",
                        Mitigation = "Backup column data or migrate to another column before dropping."
                    });
                }
            }

            // 3. Modified columns - RISKY or DESTRUCTIVE
            foreach (var (colName, sourceCol) in sourceColumns)
            {
                if (!targetColumns.TryGetValue(colName, out var targetCol))
                    continue;

                if (ColumnsEqual(sourceCol, targetCol, options))
                    continue;

                var changeAnalysis = AnalyzeColumnChange(sourceCol, targetCol, options);

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.ModifyColumns,
                    OperationType: DiffOperationType.ModifyColumn,
                    TableName: tableName,
                    ObjectName: colName,
                    Description: $"Modify column {tableName}.{colName}",
                    IsRisky: changeAnalysis.IsRisky,
                    IsDestructive: changeAnalysis.IsDestructive
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = changeAnalysis.IsDestructive
                        ? DiffWarningType.DestructiveChange
                        : changeAnalysis.IsRisky
                            ? DiffWarningType.RiskyChange
                            : DiffWarningType.RequiresManualReview,
                    Table = tableName,
                    ObjectName = colName,
                    Message = changeAnalysis.Description,
                    Mitigation = changeAnalysis.Mitigation
                });
            }
        }
    }

    private static bool ColumnsEqual(ColumnDef a, ColumnDef b, DiffOptions options)
    {
        if (a.ColumnType != b.ColumnType)
            return false;

        if (a.IsNullable != b.IsNullable)
            return false;

        if (!options.IgnoreDefaults && a.Default != b.Default)
            return false;

        if (!options.IgnoreExtra && a.Extra != b.Extra)
            return false;

        if (!options.IgnoreCharsetCollation)
        {
            if (a.Charset != b.Charset)
                return false;
            if (a.Collation != b.Collation)
                return false;
        }

        return true;
    }

    private sealed record ColumnChangeAnalysis(
        bool IsRisky,
        bool IsDestructive,
        string Description,
        string? Mitigation
    );

    private static ColumnChangeAnalysis AnalyzeColumnChange(
        ColumnDef source,
        ColumnDef target,
        DiffOptions options)
    {
        var changes = new List<string>();
        var isDestructive = false;
        var isRisky = false;
        string? mitigation = null;

        // Type changes
        if (source.ColumnType != target.ColumnType)
        {
            changes.Add($"Type: {source.ColumnType} → {target.ColumnType}");

            if (IsTypeShrink(source.ColumnType, target.ColumnType))
            {
                isDestructive = true;
                mitigation = "Data may be truncated. Run precheck to identify affected rows.";
            }
            else if (IsTypeIncompatible(source.ColumnType, target.ColumnType))
            {
                isRisky = true;
                mitigation = "Type conversion may fail for some values.";
            }
            else if (options.StrictColumnTypeChanges)
            {
                isDestructive = true;
            }
        }

        // Nullability changes
        if (source.IsNullable != target.IsNullable)
        {
            changes.Add($"Nullable: {source.IsNullable} → {target.IsNullable}");

            if (source.IsNullable && !target.IsNullable)
            {
                isDestructive = true;
                mitigation = "Existing NULL values will cause migration to fail. Run precheck first.";
            }
        }

        // Default changes
        if (source.Default != target.Default)
        {
            changes.Add($"Default: {source.Default ?? "NULL"} → {target.Default ?? "NULL"}");
        }

        // Charset/collation changes
        if (source.Charset != target.Charset)
        {
            changes.Add($"Charset: {source.Charset} → {target.Charset}");
            isRisky = true;
            mitigation ??= "Charset change may cause data truncation for multibyte characters.";
        }

        if (source.Collation != target.Collation)
        {
            changes.Add($"Collation: {source.Collation} → {target.Collation}");
        }

        // Extra changes
        if (source.Extra != target.Extra)
        {
            changes.Add($"Extra: {source.Extra} → {target.Extra}");

            // Removing auto_increment is risky
            if (source.IsAutoIncrement && !target.IsAutoIncrement)
            {
                isRisky = true;
            }
        }

        return new ColumnChangeAnalysis(
            isRisky || isDestructive,
            isDestructive,
            string.Join("; ", changes),
            mitigation
        );
    }

    private static bool IsTypeShrink(string sourceType, string targetType)
    {
        // VARCHAR shrink
        if (sourceType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase) &&
            targetType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractSize(targetType) < ExtractSize(sourceType);
        }

        // CHAR shrink
        if (sourceType.StartsWith("char", StringComparison.OrdinalIgnoreCase) &&
            targetType.StartsWith("char", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractSize(targetType) < ExtractSize(sourceType);
        }

        // INT family shrink: bigint > int > mediumint > smallint > tinyint
        var intSizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["tinyint"] = 1,
            ["smallint"] = 2,
            ["mediumint"] = 3,
            ["int"] = 4,
            ["bigint"] = 5
        };

        var sourceInt = intSizes.Keys.FirstOrDefault(k => sourceType.StartsWith(k, StringComparison.OrdinalIgnoreCase));
        var targetInt = intSizes.Keys.FirstOrDefault(k => targetType.StartsWith(k, StringComparison.OrdinalIgnoreCase));

        if (sourceInt != null && targetInt != null)
        {
            return intSizes[targetInt] < intSizes[sourceInt];
        }

        // DECIMAL/NUMERIC precision shrink
        if ((sourceType.StartsWith("decimal", StringComparison.OrdinalIgnoreCase) ||
             sourceType.StartsWith("numeric", StringComparison.OrdinalIgnoreCase)) &&
            (targetType.StartsWith("decimal", StringComparison.OrdinalIgnoreCase) ||
             targetType.StartsWith("numeric", StringComparison.OrdinalIgnoreCase)))
        {
            var (sourcePrecision, sourceScale) = ExtractDecimalPrecision(sourceType);
            var (targetPrecision, targetScale) = ExtractDecimalPrecision(targetType);

            return targetPrecision < sourcePrecision || targetScale < sourceScale;
        }

        return false;
    }

    private static bool IsTypeIncompatible(string sourceType, string targetType)
    {
        // Text to numeric (or vice versa) is risky
        var isSourceText = IsTextType(sourceType);
        var isTargetText = IsTextType(targetType);
        var isSourceNumeric = IsNumericType(sourceType);
        var isTargetNumeric = IsNumericType(targetType);

        if (isSourceText && isTargetNumeric)
            return true;

        if (isSourceNumeric && isTargetText)
            return false; // Usually safe, just formatting

        return false;
    }

    private static bool IsTextType(string type)
    {
        var textTypes = new[] { "char", "varchar", "text", "tinytext", "mediumtext", "longtext", "enum", "set" };
        return textTypes.Any(t => type.StartsWith(t, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNumericType(string type)
    {
        var numericTypes = new[] { "int", "tinyint", "smallint", "mediumint", "bigint", "decimal", "numeric", "float", "double", "real" };
        return numericTypes.Any(t => type.StartsWith(t, StringComparison.OrdinalIgnoreCase));
    }

    private static int ExtractSize(string type)
    {
        var match = Regex.Match(type, @"\((\d+)\)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var size) ? size : int.MaxValue;
    }

    private static (int Precision, int Scale) ExtractDecimalPrecision(string type)
    {
        var match = Regex.Match(type, @"\((\d+)(?:,\s*(\d+))?\)");
        if (!match.Success)
            return (int.MaxValue, int.MaxValue);

        var precision = int.Parse(match.Groups[1].Value);
        var scale = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;

        return (precision, scale);
    }

    // ========================================
    // INDEX DIFFING
    // ========================================

    private static void DiffIndexes(
        Dictionary<string, TableDef> source,
        Dictionary<string, TableDef> target,
        DiffPlan plan,
        DiffOptions options)
    {
        if (options.IgnoreIndexes)
            return;

        foreach (var (tableName, sourceTable) in source)
        {
            if (!target.TryGetValue(tableName, out var targetTable))
                continue;

            var sourceIndexes = sourceTable.Indexes;
            var targetIndexes = targetTable.Indexes;

            // 1. Added indexes - SAFE
            foreach (var (indexName, targetIndex) in targetIndexes)
            {
                if (!sourceIndexes.ContainsKey(indexName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.AddIndexes,
                        OperationType: DiffOperationType.AddIndex,
                        TableName: tableName,
                        ObjectName: indexName,
                        Description: $"Add index {tableName}.{indexName}",
                        IsRisky: false,
                        IsDestructive: false
                    ));

                    if (targetIndex.IsUnique)
                    {
                        plan.AddWarning(new DiffWarning
                        {
                            Type = DiffWarningType.RiskyChange,
                            Table = tableName,
                            ObjectName = indexName,
                            Message = "Adding unique index may fail if duplicate values exist.",
                            Mitigation = "Run precheck for duplicates before migration."
                        });
                    }
                }
            }

            // 2. Removed indexes - DESTRUCTIVE
            foreach (var (indexName, sourceIndex) in sourceIndexes)
            {
                if (!targetIndexes.ContainsKey(indexName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.Drops,
                        OperationType: DiffOperationType.DropIndex,
                        TableName: tableName,
                        ObjectName: indexName,
                        Description: $"Drop index {tableName}.{indexName}",
                        IsRisky: false,
                        IsDestructive: true
                    ));

                    plan.AddWarning(new DiffWarning
                    {
                        Type = sourceIndex.IsUnique
                            ? DiffWarningType.DestructiveChange
                            : DiffWarningType.PerformanceImpact,
                        Table = tableName,
                        ObjectName = indexName,
                        Message = sourceIndex.IsUnique
                            ? "Dropping unique index removes uniqueness constraint."
                            : "Dropping index may impact query performance."
                    });
                }
            }

            // 3. Modified indexes - DROP + ADD
            foreach (var (indexName, sourceIndex) in sourceIndexes)
            {
                if (!targetIndexes.TryGetValue(indexName, out var targetIndex))
                    continue;

                if (IndexesEqual(sourceIndex, targetIndex))
                    continue;

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.Drops,
                    OperationType: DiffOperationType.DropIndex,
                    TableName: tableName,
                    ObjectName: indexName,
                    Description: $"Drop index {tableName}.{indexName} (definition changed)",
                    IsRisky: true,
                    IsDestructive: true
                ));

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.AddIndexes,
                    OperationType: DiffOperationType.AddIndex,
                    TableName: tableName,
                    ObjectName: indexName,
                    Description: $"Add index {tableName}.{indexName} (definition changed)",
                    IsRisky: true,
                    IsDestructive: false
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.RiskyChange,
                    Table = tableName,
                    ObjectName = indexName,
                    Message = BuildIndexChangeMessage(sourceIndex, targetIndex)
                });
            }
        }
    }

    private static bool IndexesEqual(IndexDef a, IndexDef b)
    {
        if (a.IsUnique != b.IsUnique)
            return false;

        if (!string.Equals(a.IndexType, b.IndexType, StringComparison.OrdinalIgnoreCase))
            return false;

        if (a.Columns.Count != b.Columns.Count)
            return false;

        for (int i = 0; i < a.Columns.Count; i++)
        {
            if (!string.Equals(a.Columns[i], b.Columns[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string BuildIndexChangeMessage(IndexDef source, IndexDef target)
    {
        var parts = new List<string>();

        if (source.IsUnique != target.IsUnique)
            parts.Add($"Unique: {source.IsUnique} → {target.IsUnique}");

        if (!string.Equals(source.IndexType, target.IndexType, StringComparison.OrdinalIgnoreCase))
            parts.Add($"Type: {source.IndexType} → {target.IndexType}");

        if (!source.Columns.SequenceEqual(target.Columns, StringComparer.OrdinalIgnoreCase))
            parts.Add($"Columns: ({string.Join(", ", source.Columns)}) → ({string.Join(", ", target.Columns)})");

        return string.Join("; ", parts);
    }

    // ========================================
    // FOREIGN KEY DIFFING
    // ========================================

    private static void DiffForeignKeys(
        Dictionary<string, TableDef> source,
        Dictionary<string, TableDef> target,
        DiffPlan plan,
        DiffOptions options)
    {
        if (options.IgnoreForeignKeys)
            return;

        foreach (var (tableName, sourceTable) in source)
        {
            if (!target.TryGetValue(tableName, out var targetTable))
                continue;

            var sourceFks = sourceTable.ForeignKeys;
            var targetFks = targetTable.ForeignKeys;

            // 1. Added FKs - RISKY
            foreach (var (fkName, targetFk) in targetFks)
            {
                if (!sourceFks.ContainsKey(fkName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.AddForeignKeys,
                        OperationType: DiffOperationType.AddForeignKey,
                        TableName: tableName,
                        ObjectName: fkName,
                        Description: $"Add foreign key {tableName}.{fkName}",
                        IsRisky: true,
                        IsDestructive: false
                    ));

                    plan.AddWarning(new DiffWarning
                    {
                        Type = DiffWarningType.IntegrityRisk,
                        Table = tableName,
                        ObjectName = fkName,
                        Message = $"Adding FK referencing {targetFk.RefTable} may fail if orphan rows exist.",
                        Mitigation = "Run orphan check prevalidation before migration."
                    });
                }
            }

            // 2. Removed FKs - DESTRUCTIVE
            foreach (var (fkName, sourceFk) in sourceFks)
            {
                if (!targetFks.ContainsKey(fkName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.Drops,
                        OperationType: DiffOperationType.DropForeignKey,
                        TableName: tableName,
                        ObjectName: fkName,
                        Description: $"Drop foreign key {tableName}.{fkName}",
                        IsRisky: false,
                        IsDestructive: true
                    ));

                    plan.AddWarning(new DiffWarning
                    {
                        Type = DiffWarningType.IntegrityRisk,
                        Table = tableName,
                        ObjectName = fkName,
                        Message = "Removing FK — referential integrity will no longer be enforced."
                    });
                }
            }

            // 3. Modified FKs - DROP + ADD
            foreach (var (fkName, sourceFk) in sourceFks)
            {
                if (!targetFks.TryGetValue(fkName, out var targetFk))
                    continue;

                if (ForeignKeysEqual(sourceFk, targetFk))
                    continue;

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.Drops,
                    OperationType: DiffOperationType.DropForeignKey,
                    TableName: tableName,
                    ObjectName: fkName,
                    Description: $"Drop foreign key {tableName}.{fkName} (definition changed)",
                    IsRisky: true,
                    IsDestructive: true
                ));

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.AddForeignKeys,
                    OperationType: DiffOperationType.AddForeignKey,
                    TableName: tableName,
                    ObjectName: fkName,
                    Description: $"Add foreign key {tableName}.{fkName} (definition changed)",
                    IsRisky: true,
                    IsDestructive: false
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.RiskyChange,
                    Table = tableName,
                    ObjectName = fkName,
                    Message = BuildForeignKeyChangeMessage(sourceFk, targetFk)
                });
            }
        }
    }

    private static bool ForeignKeysEqual(ForeignKeyDef a, ForeignKeyDef b)
    {
        if (!string.Equals(a.RefTable, b.RefTable, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(a.OnDelete, b.OnDelete, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(a.OnUpdate, b.OnUpdate, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!a.Columns.SequenceEqual(b.Columns, StringComparer.OrdinalIgnoreCase))
            return false;

        if (!a.RefColumns.SequenceEqual(b.RefColumns, StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static string BuildForeignKeyChangeMessage(ForeignKeyDef source, ForeignKeyDef target)
    {
        var parts = new List<string>();

        if (!string.Equals(source.RefTable, target.RefTable, StringComparison.OrdinalIgnoreCase))
            parts.Add($"RefTable: {source.RefTable} → {target.RefTable}");

        if (!source.Columns.SequenceEqual(target.Columns, StringComparer.OrdinalIgnoreCase))
            parts.Add($"Columns: ({string.Join(", ", source.Columns)}) → ({string.Join(", ", target.Columns)})");

        if (!source.RefColumns.SequenceEqual(target.RefColumns, StringComparer.OrdinalIgnoreCase))
            parts.Add($"RefColumns: ({string.Join(", ", source.RefColumns)}) → ({string.Join(", ", target.RefColumns)})");

        if (!string.Equals(source.OnDelete, target.OnDelete, StringComparison.OrdinalIgnoreCase))
            parts.Add($"OnDelete: {source.OnDelete} → {target.OnDelete}");

        if (!string.Equals(source.OnUpdate, target.OnUpdate, StringComparison.OrdinalIgnoreCase))
            parts.Add($"OnUpdate: {source.OnUpdate} → {target.OnUpdate}");

        return string.Join("; ", parts);
    }
}

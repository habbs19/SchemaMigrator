using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;
using SchemaMigrator.Core.Sql;

namespace SchemaMigrator.Core.Diff;

public sealed class SchemaDiffer
{
    public DiffPlan Diff(
        SchemaSnapshot prod,
        SchemaSnapshot dev,
        DiffOptions options)
    {
        if (prod == null) throw new ArgumentNullException(nameof(prod));
        if (dev == null) throw new ArgumentNullException(nameof(dev));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var plan = new DiffPlan();

        DiffTables(prod, dev, plan, options);
        DiffColumns(prod, dev, plan, options);
        DiffIndexes(prod, dev, plan, options);
        DiffForeignKeys(prod, dev, plan, options);

        return plan;
    }

    // ---- private diff stages (one responsibility each) ----

    private static void DiffTables(
     SchemaSnapshot prod,
     SchemaSnapshot dev,
     DiffPlan plan,
     DiffOptions options)
    {
        var prodTables = prod.Tables;
        var devTables = dev.Tables;

        // 1️⃣ Added tables (SAFE)
        foreach (var (tableName, devTable) in devTables)
        {
            if (!prodTables.ContainsKey(tableName))
            {
                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.CreateTables,
                    Description: $"Create table {tableName}",
                    IsRisky: false,
                    IsDestructive: false
                ));
            }
        }

        // 2️⃣ Removed tables (DESTRUCTIVE)
        foreach (var (tableName, prodTable) in prodTables)
        {
            if (!devTables.ContainsKey(tableName))
            {
                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.Drops,
                    Description: $"Drop table {tableName}",
                    IsRisky: false,
                    IsDestructive: true
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.DestructiveChange,
                    Table = tableName,
                    ObjectName = null,
                    Message = "Table removed — ALL data in this table will be permanently lost."
                });
            }
        }
    }


    private static void DiffColumns(
      SchemaSnapshot prod,
      SchemaSnapshot dev,
      DiffPlan plan,
      DiffOptions options)
    {
        foreach (var (tableName, prodTable) in prod.Tables)
        {
            if (!dev.Tables.TryGetValue(tableName, out var devTable))
                continue; // table drop handled in DiffTables

            var prodColumns = prodTable.Columns;
            var devColumns = devTable.Columns;

            // 1️⃣ Added columns (SAFE)
            foreach (var (colName, devCol) in devColumns)
            {
                if (!prodColumns.ContainsKey(colName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.AddColumns,
                        Description: $"Add column {tableName}.{colName}",
                        IsRisky: false,
                        IsDestructive: false
                    ));

                    continue;
                }
            }

            // 2️⃣ Removed columns (DESTRUCTIVE)
            foreach (var (colName, prodCol) in prodColumns)
            {
                if (!devColumns.ContainsKey(colName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.Drops,
                        Description: $"Drop column {tableName}.{colName}",
                        IsRisky: false,
                        IsDestructive: true
                    ));

                    plan.AddWarning(new DiffWarning
                    {
                        Type = DiffWarningType.DestructiveChange,
                        Table = tableName,
                        ObjectName = colName,
                        Message = "Column removed — data will be lost."
                    });
                }
            }

            // 3️⃣ Modified columns (RISKY / DESTRUCTIVE)
            foreach (var (colName, prodCol) in prodColumns)
            {
                if (!devColumns.TryGetValue(colName, out var devCol))
                    continue;

                if (ColumnsEqual(prodCol, devCol))
                    continue;

                var isDestructive = IsDestructiveColumnChange(prodCol, devCol);
                var isRisky = !isDestructive;

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.ModifyColumns,
                    Description: $"Modify column {tableName}.{colName}",
                    IsRisky: isRisky,
                    IsDestructive: isDestructive
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = isDestructive
                        ? DiffWarningType.DestructiveChange
                        : DiffWarningType.RiskyChange,
                    Table = tableName,
                    ObjectName = colName,
                    Message = BuildColumnChangeMessage(prodCol, devCol)
                });
            }
        }
    }
    private static bool ColumnsEqual(ColumnDef a, ColumnDef b)
    {
        return
            a.ColumnType == b.ColumnType &&
            a.IsNullable == b.IsNullable &&
            a.Default == b.Default &&
            a.Extra == b.Extra &&
            a.Charset == b.Charset &&
            a.Collation == b.Collation;
    }
    private static bool IsDestructiveColumnChange(ColumnDef prod, ColumnDef dev)
    {
        // Nullable → NOT NULL
        if (prod.IsNullable && !dev.IsNullable)
            return true;

        // Type shrink (very conservative)
        if (IsTypeShrink(prod.ColumnType, dev.ColumnType))
            return true;

        return false;
    }
    private static bool IsTypeShrink(string prodType, string devType)
    {
        // Only detect obvious varchar shrink
        if (!prodType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase) ||
            !devType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase))
            return false;

        int prodSize = ExtractVarcharSize(prodType);
        int devSize = ExtractVarcharSize(devType);

        return devSize < prodSize;
    }
    private static string BuildColumnChangeMessage(ColumnDef prod, ColumnDef dev)
    {
        var parts = new List<string>();

        if (prod.ColumnType != dev.ColumnType)
            parts.Add($"Type: {prod.ColumnType} → {dev.ColumnType}");

        if (prod.IsNullable != dev.IsNullable)
            parts.Add($"Nullable: {prod.IsNullable} → {dev.IsNullable}");

        if (prod.Default != dev.Default)
            parts.Add($"Default: {prod.Default ?? "NULL"} → {dev.Default ?? "NULL"}");

        if (prod.Charset != dev.Charset)
            parts.Add($"Charset: {prod.Charset} → {dev.Charset}");

        if (prod.Collation != dev.Collation)
            parts.Add($"Collation: {prod.Collation} → {dev.Collation}");

        if (prod.Extra != dev.Extra)
            parts.Add($"Extra: {prod.Extra} → {dev.Extra}");

        return string.Join("; ", parts);
    }

    private static int ExtractVarcharSize(string type)
    {
        var start = type.IndexOf('(');
        var end = type.IndexOf(')');

        if (start < 0 || end < 0 || end <= start)
            return int.MaxValue;

        if (int.TryParse(type.Substring(start + 1, end - start - 1), out var size))
            return size;

        return int.MaxValue;
    }


    private static void DiffIndexes(
    SchemaSnapshot prod,
    SchemaSnapshot dev,
    DiffPlan plan,
    DiffOptions options)
    {
        if(options.IgnoreIndexes)
            return;

        foreach (var (tableName, prodTable) in prod.Tables)
        {
            if (!dev.Tables.TryGetValue(tableName, out var devTable))
                continue; // table drop handled elsewhere

            var prodIndexes = prodTable.Indexes;
            var devIndexes = devTable.Indexes;

            // 1️⃣ Added indexes (SAFE)
            foreach (var (indexName, devIndex) in devIndexes)
            {
                if (!prodIndexes.ContainsKey(indexName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.AddIndexes,
                        Description: $"Add index {tableName}.{indexName}",
                        IsRisky: false,
                        IsDestructive: false
                    ));
                }
            }

            // 2️⃣ Removed indexes (DESTRUCTIVE)
            foreach (var (indexName, prodIndex) in prodIndexes)
            {
                if (!devIndexes.ContainsKey(indexName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.Drops,
                        Description: $"Drop index {tableName}.{indexName}",
                        IsRisky: false,
                        IsDestructive: true
                    ));

                    plan.AddWarning(new DiffWarning
                    {
                        Type = DiffWarningType.DestructiveChange,
                        Table = tableName,
                        ObjectName = indexName,
                        Message = "Index removed — may impact query performance or uniqueness guarantees."
                    });
                }
            }

            // 3️⃣ Modified indexes (RISKY: DROP + ADD)
            foreach (var (indexName, prodIndex) in prodIndexes)
            {
                if (!devIndexes.TryGetValue(indexName, out var devIndex))
                    continue;

                if (IndexesEqual(prodIndex, devIndex))
                    continue;

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.Drops,
                    Description: $"Drop index {tableName}.{indexName} (definition changed)",
                    IsRisky: true,
                    IsDestructive: true
                ));

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.AddIndexes,
                    Description: $"Add index {tableName}.{indexName} (definition changed)",
                    IsRisky: true,
                    IsDestructive: false
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.RiskyChange,
                    Table = tableName,
                    ObjectName = indexName,
                    Message = BuildIndexChangeMessage(prodIndex, devIndex)
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
    private static string BuildIndexChangeMessage(IndexDef prod, IndexDef dev)
    {
        var parts = new List<string>();

        if (prod.IsUnique != dev.IsUnique)
            parts.Add($"Unique: {prod.IsUnique} → {dev.IsUnique}");

        if (!string.Equals(prod.IndexType, dev.IndexType, StringComparison.OrdinalIgnoreCase))
            parts.Add($"Type: {prod.IndexType} → {dev.IndexType}");

        if (!prod.Columns.SequenceEqual(dev.Columns, StringComparer.OrdinalIgnoreCase))
            parts.Add($"Columns: ({string.Join(", ", prod.Columns)}) → ({string.Join(", ", dev.Columns)})");

        return string.Join("; ", parts);
    }


    private static void DiffForeignKeys(
      SchemaSnapshot prod,
      SchemaSnapshot dev,
      DiffPlan plan,
      DiffOptions options)
    {
        if (options.IgnoreForeignKeys)
            return;

        foreach (var (tableName, prodTable) in prod.Tables)
        {
            if (!dev.Tables.TryGetValue(tableName, out var devTable))
                continue; // table drop handled in DiffTables

            var prodFks = prodTable.ForeignKeys;
            var devFks = devTable.ForeignKeys;

            // 1️⃣ Added FKs (RISKY)
            foreach (var (fkName, devFk) in devFks)
            {
                if (!prodFks.ContainsKey(fkName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.AddForeignKeys,
                        Description: $"Add foreign key {tableName}.{fkName}",
                        IsRisky: true,
                        IsDestructive: false
                    ));

                    plan.AddWarning(new DiffWarning
                    {
                        Type = DiffWarningType.RiskyChange,
                        Table = tableName,
                        ObjectName = fkName,
                        Message = "Adding a foreign key may fail if existing data contains orphan rows. Prechecks are required."
                    });
                }
            }

            // 2️⃣ Removed FKs (DESTRUCTIVE)
            foreach (var (fkName, prodFk) in prodFks)
            {
                if (!devFks.ContainsKey(fkName))
                {
                    plan.AddOperation(new DiffOperation(
                        Phase: SqlPhase.Drops,
                        Description: $"Drop foreign key {tableName}.{fkName}",
                        IsRisky: false,
                        IsDestructive: true
                    ));

                    plan.AddWarning(new DiffWarning
                    {
                        Type = DiffWarningType.DestructiveChange,
                        Table = tableName,
                        ObjectName = fkName,
                        Message = "Foreign key removed — referential integrity will no longer be enforced."
                    });
                }
            }

            // 3️⃣ Changed FKs (RISKY: DROP + ADD)
            foreach (var (fkName, prodFk) in prodFks)
            {
                if (!devFks.TryGetValue(fkName, out var devFk))
                    continue;

                if (ForeignKeysEqual(prodFk, devFk))
                    continue;

                // MySQL FK changes require drop + add
                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.Drops,
                    Description: $"Drop foreign key {tableName}.{fkName} (definition changed)",
                    IsRisky: true,
                    IsDestructive: true
                ));

                plan.AddOperation(new DiffOperation(
                    Phase: SqlPhase.AddForeignKeys,
                    Description: $"Add foreign key {tableName}.{fkName} (definition changed)",
                    IsRisky: true,
                    IsDestructive: false
                ));

                plan.AddWarning(new DiffWarning
                {
                    Type = DiffWarningType.RiskyChange,
                    Table = tableName,
                    ObjectName = fkName,
                    Message = BuildForeignKeyChangeMessage(prodFk, devFk)
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

        if (a.Columns.Count != b.Columns.Count || a.RefColumns.Count != b.RefColumns.Count)
            return false;

        for (int i = 0; i < a.Columns.Count; i++)
        {
            if (!string.Equals(a.Columns[i], b.Columns[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        for (int i = 0; i < a.RefColumns.Count; i++)
        {
            if (!string.Equals(a.RefColumns[i], b.RefColumns[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string BuildForeignKeyChangeMessage(ForeignKeyDef prod, ForeignKeyDef dev)
    {
        var parts = new List<string>();

        if (!string.Equals(prod.RefTable, dev.RefTable, StringComparison.OrdinalIgnoreCase))
            parts.Add($"RefTable: {prod.RefTable} → {dev.RefTable}");

        if (!prod.Columns.SequenceEqual(dev.Columns, StringComparer.OrdinalIgnoreCase))
            parts.Add($"Columns: ({string.Join(", ", prod.Columns)}) → ({string.Join(", ", dev.Columns)})");

        if (!prod.RefColumns.SequenceEqual(dev.RefColumns, StringComparer.OrdinalIgnoreCase))
            parts.Add($"RefColumns: ({string.Join(", ", prod.RefColumns)}) → ({string.Join(", ", dev.RefColumns)})");

        if (!string.Equals(prod.OnDelete, dev.OnDelete, StringComparison.OrdinalIgnoreCase))
            parts.Add($"OnDelete: {prod.OnDelete} → {dev.OnDelete}");

        if (!string.Equals(prod.OnUpdate, dev.OnUpdate, StringComparison.OrdinalIgnoreCase))
            parts.Add($"OnUpdate: {prod.OnUpdate} → {dev.OnUpdate}");

        return string.Join("; ", parts);
    }

}

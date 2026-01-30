using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Sql;
using System.Globalization;
using System.Text;

namespace SchemaMigrator.MySql;

/// <summary>
/// MySQL-specific SQL emitter supporting MySQL 5.7+ and 8.0+.
/// </summary>
public sealed class MySqlSqlEmitter : SqlEmitterBase
{
    protected override void EmitPreparation(EmitContext context)
    {
        if (context.Options.IncludeComments && context.Options.IncludeTimestamp)
        {
            context.AddSql($"-- Migration started at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        }

        if (context.Options.LockWaitTimeout.HasValue)
        {
            context.AddSql($"SET lock_wait_timeout = {context.Options.LockWaitTimeout.Value};");
        }

        if (context.Options.DisableForeignKeyChecks)
        {
            context.AddSql("SET FOREIGN_KEY_CHECKS = 0;");
            if (context.Options.GenerateRollback)
                context.AddRollback("SET FOREIGN_KEY_CHECKS = 1;");
        }

        if (context.Options.UseTransaction)
        {
            context.AddSql("START TRANSACTION;");
        }
    }

    protected override void EmitCleanup(EmitContext context)
    {
        if (context.Options.UseTransaction)
        {
            context.AddSql("COMMIT;");
        }

        if (context.Options.DisableForeignKeyChecks)
        {
            context.AddSql("SET FOREIGN_KEY_CHECKS = 1;");
        }
    }

    protected override void EmitOperation(DiffOperation operation, EmitContext context)
    {
        if (context.Options.IncludeComments)
            context.AddComment(operation.Description);

        switch (operation.OperationType)
        {
            case DiffOperationType.CreateTable:
                EmitCreateTable(operation, context);
                break;

            case DiffOperationType.DropTable:
                EmitDropTable(operation, context);
                break;

            case DiffOperationType.AddColumn:
                EmitAddColumn(operation, context);
                break;

            case DiffOperationType.DropColumn:
                EmitDropColumn(operation, context);
                break;

            case DiffOperationType.ModifyColumn:
                EmitModifyColumn(operation, context);
                break;

            case DiffOperationType.AddIndex:
                EmitAddIndex(operation, context);
                break;

            case DiffOperationType.DropIndex:
                EmitDropIndex(operation, context);
                break;

            case DiffOperationType.AddForeignKey:
                EmitAddForeignKey(operation, context);
                break;

            case DiffOperationType.DropForeignKey:
                EmitDropForeignKey(operation, context);
                break;

            case DiffOperationType.AddPrimaryKey:
                EmitAddPrimaryKey(operation, context);
                break;

            case DiffOperationType.DropPrimaryKey:
                EmitDropPrimaryKey(operation, context);
                break;

            default:
                context.AddWarning($"Unsupported operation: {operation.OperationType}");
                context.Skip(operation);
                break;
        }
    }

    // =====================================================
    // CREATE TABLE
    // =====================================================

    private void EmitCreateTable(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var table = context.Target.Tables[tableName];

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE `{tableName}` (");

        var definitions = new List<string>();

        // Columns in ordinal order
        foreach (var column in table.ColumnsOrdered)
        {
            definitions.Add("  " + RenderColumnDefinition(column));
        }

        // Primary key
        if (table.PrimaryKey != null)
        {
            definitions.Add(
                $"  PRIMARY KEY ({string.Join(", ", table.PrimaryKey.Columns.Select(c => $"`{c}`"))})");
        }

        // Unique indexes inline
        foreach (var index in table.Indexes.Values.Where(i => i.IsUnique))
        {
            definitions.Add(
                $"  UNIQUE KEY `{index.Name}` ({string.Join(", ", index.Columns.Select(c => $"`{c}`"))})");
        }

        sb.AppendLine(string.Join(",\n", definitions));
        sb.Append(')');

        // Table options
        var tableOptions = new List<string>();
        if (!string.IsNullOrEmpty(table.Engine))
            tableOptions.Add($"ENGINE={table.Engine}");
        else
            tableOptions.Add("ENGINE=InnoDB");

        if (!string.IsNullOrEmpty(table.Charset))
            tableOptions.Add($"DEFAULT CHARSET={table.Charset}");

        if (!string.IsNullOrEmpty(table.Collation))
            tableOptions.Add($"COLLATE={table.Collation}");

        if (tableOptions.Count > 0)
            sb.Append(" " + string.Join(" ", tableOptions));

        sb.Append(';');

        context.AddSql(sb.ToString());

        // Rollback
        if (context.Options.GenerateRollback)
        {
            context.AddRollback($"DROP TABLE IF EXISTS `{tableName}`;");
        }
    }

    // =====================================================
    // DROP TABLE
    // =====================================================

    private void EmitDropTable(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;

        if (context.Options.UseConditionalStatements)
            context.AddSql($"DROP TABLE IF EXISTS `{tableName}`;");
        else
            context.AddSql($"DROP TABLE `{tableName}`;");

        // Rollback for DROP TABLE requires recreating the entire table - mark as info
        if (context.Options.GenerateRollback)
        {
            context.AddRollback($"-- WARNING: Cannot auto-generate rollback for DROP TABLE `{tableName}` - data is lost");
        }
    }

    // =====================================================
    // ADD COLUMN
    // =====================================================

    private void EmitAddColumn(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var columnName = op.ObjectName!;
        var column = context.Target.Tables[tableName].Columns[columnName];

        var afterClause = GetAfterClause(column, context.Target.Tables[tableName]);

        context.AddSql(
            $"ALTER TABLE `{tableName}` ADD COLUMN {RenderColumnDefinition(column)}{afterClause};");

        if (context.Options.GenerateRollback)
        {
            context.AddRollback($"ALTER TABLE `{tableName}` DROP COLUMN `{columnName}`;");
        }
    }

    private static string GetAfterClause(ColumnDef column, TableDef table)
    {
        var orderedColumns = table.ColumnsOrdered.ToList();
        var index = orderedColumns.FindIndex(c => c.Name.Equals(column.Name, StringComparison.OrdinalIgnoreCase));

        if (index <= 0)
            return " FIRST";

        var previousColumn = orderedColumns[index - 1];
        return $" AFTER `{previousColumn.Name}`";
    }

    // =====================================================
    // DROP COLUMN
    // =====================================================

    private void EmitDropColumn(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var columnName = op.ObjectName!;

        context.AddSql($"ALTER TABLE `{tableName}` DROP COLUMN `{columnName}`;");

        if (context.Options.GenerateRollback)
        {
            var column = context.Source.Tables[tableName].Columns[columnName];
            context.AddRollback(
                $"ALTER TABLE `{tableName}` ADD COLUMN {RenderColumnDefinition(column)};");
        }
    }

    // =====================================================
    // MODIFY COLUMN
    // =====================================================

    private void EmitModifyColumn(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var columnName = op.ObjectName!;
        var sourceCol = context.Source.Tables[tableName].Columns[columnName];
        var targetCol = context.Target.Tables[tableName].Columns[columnName];

        // Prechecks
        if (sourceCol.IsNullable && !targetCol.IsNullable)
        {
            context.AddPrecheck(
                $"-- Check for NULL values before NOT NULL constraint\n" +
                $"SELECT COUNT(*) AS NullCount FROM `{tableName}` WHERE `{columnName}` IS NULL;");
        }

        // Type shrink check
        if (IsVarcharShrink(sourceCol.ColumnType, targetCol.ColumnType))
        {
            var newSize = ExtractVarcharSize(targetCol.ColumnType);
            context.AddPrecheck(
                $"-- Check for data that would be truncated\n" +
                $"SELECT COUNT(*) AS TruncatedCount FROM `{tableName}` WHERE CHAR_LENGTH(`{columnName}`) > {newSize};");
        }

        var alterSql = context.Options.PreferOnlineDdl
            ? $"ALTER TABLE `{tableName}` MODIFY COLUMN {RenderColumnDefinition(targetCol)}, ALGORITHM=INPLACE, LOCK=NONE;"
            : $"ALTER TABLE `{tableName}` MODIFY COLUMN {RenderColumnDefinition(targetCol)};";

        context.AddSql(alterSql);

        if (context.Options.GenerateRollback)
        {
            context.AddRollback(
                $"ALTER TABLE `{tableName}` MODIFY COLUMN {RenderColumnDefinition(sourceCol)};");
        }
    }

    private static bool IsVarcharShrink(string sourceType, string targetType)
    {
        if (!sourceType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase) ||
            !targetType.StartsWith("varchar", StringComparison.OrdinalIgnoreCase))
            return false;

        return ExtractVarcharSize(targetType) < ExtractVarcharSize(sourceType);
    }

    private static int ExtractVarcharSize(string type)
    {
        var start = type.IndexOf('(');
        var end = type.IndexOf(')');

        if (start < 0 || end <= start)
            return int.MaxValue;

        if (int.TryParse(type.AsSpan(start + 1, end - start - 1), out var size))
            return size;

        return int.MaxValue;
    }

    // =====================================================
    // ADD INDEX
    // =====================================================

    private void EmitAddIndex(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var indexName = op.ObjectName!;
        var index = context.Target.Tables[tableName].Indexes[indexName];

        // Precheck for unique index
        if (index.IsUnique)
        {
            var columns = string.Join(", ", index.Columns.Select(c => $"`{c}`"));
            context.AddPrecheck(
                $"-- Check for duplicate values before adding unique index\n" +
                $"SELECT {columns}, COUNT(*) AS cnt FROM `{tableName}` GROUP BY {columns} HAVING cnt > 1;");
        }

        var indexType = index.IsFullText ? "FULLTEXT " :
                       index.IsSpatial ? "SPATIAL " :
                       index.IsUnique ? "UNIQUE " : "";

        var columns1 = string.Join(", ", index.Columns.Select(c => $"`{c}`"));
        var usingClause = !index.IsFullText && !index.IsSpatial && !string.IsNullOrEmpty(index.IndexType)
            ? $" USING {index.IndexType}"
            : "";

        context.AddSql($"CREATE {indexType}INDEX `{indexName}` ON `{tableName}` ({columns1}){usingClause};");

        if (context.Options.GenerateRollback)
        {
            context.AddRollback($"DROP INDEX `{indexName}` ON `{tableName}`;");
        }
    }

    // =====================================================
    // DROP INDEX
    // =====================================================

    private void EmitDropIndex(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var indexName = op.ObjectName!;

        context.AddSql($"DROP INDEX `{indexName}` ON `{tableName}`;");

        if (context.Options.GenerateRollback)
        {
            var index = context.Source.Tables[tableName].Indexes[indexName];
            var indexType = index.IsUnique ? "UNIQUE " : "";
            var columns = string.Join(", ", index.Columns.Select(c => $"`{c}`"));

            context.AddRollback($"CREATE {indexType}INDEX `{indexName}` ON `{tableName}` ({columns});");
        }
    }

    // =====================================================
    // ADD FOREIGN KEY
    // =====================================================

    private void EmitAddForeignKey(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var fkName = op.ObjectName!;
        var fk = context.Target.Tables[tableName].ForeignKeys[fkName];

        // Orphan check
        var joinConditions = fk.Columns.Zip(fk.RefColumns,
            (c, r) => $"t.`{c}` = r.`{r}`");
        var nullChecks = fk.Columns.Select(c => $"t.`{c}` IS NOT NULL");
        var refNullChecks = fk.RefColumns.Select(r => $"r.`{r}` IS NULL");

        context.AddPrecheck(
            $"-- Check for orphan rows before adding FK\n" +
            $"SELECT COUNT(*) AS OrphanCount FROM `{tableName}` t\n" +
            $"LEFT JOIN `{fk.RefTable}` r ON {string.Join(" AND ", joinConditions)}\n" +
            $"WHERE {string.Join(" AND ", nullChecks)} AND {string.Join(" AND ", refNullChecks)};");

        var columns = string.Join(", ", fk.Columns.Select(c => $"`{c}`"));
        var refColumns = string.Join(", ", fk.RefColumns.Select(c => $"`{c}`"));

        context.AddSql(
            $"ALTER TABLE `{tableName}` ADD CONSTRAINT `{fkName}` " +
            $"FOREIGN KEY ({columns}) REFERENCES `{fk.RefTable}` ({refColumns}) " +
            $"ON DELETE {fk.OnDelete} ON UPDATE {fk.OnUpdate};");

        if (context.Options.GenerateRollback)
        {
            context.AddRollback($"ALTER TABLE `{tableName}` DROP FOREIGN KEY `{fkName}`;");
        }
    }

    // =====================================================
    // DROP FOREIGN KEY
    // =====================================================

    private void EmitDropForeignKey(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var fkName = op.ObjectName!;

        context.AddSql($"ALTER TABLE `{tableName}` DROP FOREIGN KEY `{fkName}`;");

        if (context.Options.GenerateRollback)
        {
            var fk = context.Source.Tables[tableName].ForeignKeys[fkName];
            var columns = string.Join(", ", fk.Columns.Select(c => $"`{c}`"));
            var refColumns = string.Join(", ", fk.RefColumns.Select(c => $"`{c}`"));

            context.AddRollback(
                $"ALTER TABLE `{tableName}` ADD CONSTRAINT `{fkName}` " +
                $"FOREIGN KEY ({columns}) REFERENCES `{fk.RefTable}` ({refColumns}) " +
                $"ON DELETE {fk.OnDelete} ON UPDATE {fk.OnUpdate};");
        }
    }

    // =====================================================
    // PRIMARY KEY
    // =====================================================

    private void EmitAddPrimaryKey(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;
        var pk = context.Target.Tables[tableName].PrimaryKey!;

        var columns = string.Join(", ", pk.Columns.Select(c => $"`{c}`"));

        // Precheck for duplicates and nulls
        context.AddPrecheck(
            $"-- Check for NULL values in PK columns\n" +
            $"SELECT COUNT(*) AS NullCount FROM `{tableName}` WHERE " +
            string.Join(" OR ", pk.Columns.Select(c => $"`{c}` IS NULL")) + ";");

        context.AddPrecheck(
            $"-- Check for duplicate values in PK columns\n" +
            $"SELECT {columns}, COUNT(*) AS cnt FROM `{tableName}` GROUP BY {columns} HAVING cnt > 1;");

        context.AddSql($"ALTER TABLE `{tableName}` ADD PRIMARY KEY ({columns});");

        if (context.Options.GenerateRollback)
        {
            context.AddRollback($"ALTER TABLE `{tableName}` DROP PRIMARY KEY;");
        }
    }

    private void EmitDropPrimaryKey(DiffOperation op, EmitContext context)
    {
        var tableName = op.TableName;

        context.AddSql($"ALTER TABLE `{tableName}` DROP PRIMARY KEY;");

        if (context.Options.GenerateRollback)
        {
            var pk = context.Source.Tables[tableName].PrimaryKey!;
            var columns = string.Join(", ", pk.Columns.Select(c => $"`{c}`"));
            context.AddRollback($"ALTER TABLE `{tableName}` ADD PRIMARY KEY ({columns});");
        }
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private static string RenderColumnDefinition(ColumnDef c)
    {
        var sb = new StringBuilder();
        sb.Append($"`{c.Name}` {c.ColumnType}");

        // Charset/collation for string types
        if (!string.IsNullOrEmpty(c.Charset))
            sb.Append($" CHARACTER SET {c.Charset}");

        if (!string.IsNullOrEmpty(c.Collation))
            sb.Append($" COLLATE {c.Collation}");

        // Nullability
        sb.Append(c.IsNullable ? " NULL" : " NOT NULL");

        // Default value
        if (c.Default != null)
            sb.Append($" DEFAULT {FormatDefault(c.Default, c.ColumnType)}");

        // Extra (auto_increment, on update, etc)
        if (!string.IsNullOrWhiteSpace(c.Extra))
            sb.Append($" {c.Extra.ToUpperInvariant()}");

        // Comment
        if (!string.IsNullOrEmpty(c.Comment))
            sb.Append($" COMMENT '{EscapeString(c.Comment)}'");

        return sb.ToString();
    }

    private static string FormatDefault(string value, string columnType)
    {
        var v = value.Trim();

        // Expression defaults (CURRENT_TIMESTAMP, UUID(), etc)
        if (IsExpressionDefault(v))
            return v;

        // BIT literal
        if (IsBitType(columnType) && v.StartsWith("b'", StringComparison.OrdinalIgnoreCase))
            return v;

        // Numeric literal
        if (IsNumericLiteral(v))
            return v;

        // Boolean literals
        if (v.Equals("true", StringComparison.OrdinalIgnoreCase))
            return "1";
        if (v.Equals("false", StringComparison.OrdinalIgnoreCase))
            return "0";

        // NULL
        if (v.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return "NULL";

        // String literal
        return $"'{EscapeString(v)}'";
    }

    private static bool IsExpressionDefault(string value)
    {
        var expressions = new[]
        {
            "CURRENT_TIMESTAMP",
            "CURRENT_DATE",
            "CURRENT_TIME",
            "NOW()",
            "UUID()",
            "UUID_TO_BIN(UUID())"
        };

        return expressions.Any(e => value.StartsWith(e, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBitType(string columnType)
    {
        return columnType.StartsWith("bit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumericLiteral(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }

    private static string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}

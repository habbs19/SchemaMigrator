using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;
using SchemaMigrator.Core.Sql;
using System.Globalization;
using System.Text;

namespace SchemaMigrator.MySql;

public sealed class MySqlSqlEmitter : SqlEmitterBase
{
    protected override void EmitPhase(
        SqlPhase phase,
        DiffPlan plan,
        SchemaSnapshot prod,
        SchemaSnapshot dev,
        EmitOptions options,
        List<string> prechecks,
        List<string> sql)
    {
        var ops = plan.Operations.Where(o => o.Phase == phase);

        foreach (var op in ops)
        {
            switch (phase)
            {
                case SqlPhase.CreateTables:
                    EmitCreateTable(op, dev, sql);
                    break;

                case SqlPhase.AddColumns:
                    EmitAddColumn(op, dev, sql);
                    break;

                case SqlPhase.ModifyColumns:
                    EmitModifyColumn(op, prod, dev, prechecks, sql);
                    break;

                case SqlPhase.AddIndexes:
                    EmitAddIndex(op, dev, sql);
                    break;

                case SqlPhase.AddForeignKeys:
                    EmitAddForeignKey(op, dev, prechecks, sql);
                    break;

                case SqlPhase.Drops:
                    EmitDrop(op, sql);
                    break;
            }
        }
    }

    // -------------------------------------------------------
    // CREATE TABLE
    // -------------------------------------------------------

    private void EmitCreateTable(
        DiffOperation op,
        SchemaSnapshot dev,
        List<string> sql)
    {
        var tableName = ExtractTableName(op.Description);
        var table = dev.Tables[tableName];

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE `{tableName}` (");

        var columnLines = new List<string>();

        foreach (var column in table.Columns.Values)
        {
            columnLines.Add("  " + RenderColumn(column));
        }

        if (table.PrimaryKey != null)
        {
            columnLines.Add(
                $"  PRIMARY KEY ({string.Join(", ", table.PrimaryKey.Columns.Select(c => $"`{c}`"))})");
        }

        sb.AppendLine(string.Join(",\n", columnLines));
        sb.AppendLine(") ENGINE=InnoDB;");

        sql.Add(sb.ToString());
    }

    // -------------------------------------------------------
    // ADD COLUMN
    // -------------------------------------------------------

    private void EmitAddColumn(
        DiffOperation op,
        SchemaSnapshot dev,
        List<string> sql)
    {
        var (tableName, columnName) = ExtractTableAndColumn(op.Description);
        var column = dev.Tables[tableName].Columns[columnName];

        sql.Add(
            $"ALTER TABLE `{tableName}` ADD COLUMN {RenderColumn(column)};");
    }

    // -------------------------------------------------------
    // MODIFY COLUMN (+ prechecks)
    // -------------------------------------------------------

    private void EmitModifyColumn(
        DiffOperation op,
        SchemaSnapshot prod,
        SchemaSnapshot dev,
        List<string> prechecks,
        List<string> sql)
    {
        var (tableName, columnName) = ExtractTableAndColumn(op.Description);
        var prodCol = prod.Tables[tableName].Columns[columnName];
        var devCol = dev.Tables[tableName].Columns[columnName];

        // Precheck for NOT NULL
        if (prodCol.IsNullable && !devCol.IsNullable)
        {
            prechecks.Add(
                $"SELECT COUNT(*) AS NullCount FROM `{tableName}` WHERE `{columnName}` IS NULL;");
        }

        sql.Add(
            $"ALTER TABLE `{tableName}` MODIFY COLUMN {RenderColumn(devCol)};");
    }

    // -------------------------------------------------------
    // ADD INDEX
    // -------------------------------------------------------

    private void EmitAddIndex(
        DiffOperation op,
        SchemaSnapshot dev,
        List<string> sql)
    {
        var (tableName, indexName) = ExtractTableAndIndex(op.Description);
        var index = dev.Tables[tableName].Indexes[indexName];

        var unique = index.IsUnique ? "UNIQUE " : "";
        var columns = string.Join(", ", index.Columns.Select(c => $"`{c}`"));

        sql.Add(
            $"CREATE {unique}INDEX `{indexName}` ON `{tableName}` ({columns});");
    }

    // -------------------------------------------------------
    // ADD FOREIGN KEY (+ prechecks)
    // -------------------------------------------------------

    private void EmitAddForeignKey(
        DiffOperation op,
        SchemaSnapshot dev,
        List<string> prechecks,
        List<string> sql)
    {
        var (tableName, fkName) = ExtractTableAndConstraint(op.Description);
        var fk = dev.Tables[tableName].ForeignKeys[fkName];

        // Orphan check
        prechecks.Add(
            $"SELECT COUNT(*) AS Orphans FROM `{tableName}` t " +
            $"LEFT JOIN `{fk.RefTable}` r ON " +
            string.Join(" AND ",
                fk.Columns.Zip(fk.RefColumns,
                    (c, r) => $"t.`{c}` = r.`{r}`")) +
            $" WHERE " +
            string.Join(" AND ", fk.Columns.Select(c => $"t.`{c}` IS NOT NULL")) +
            " AND " +
            string.Join(" AND ", fk.RefColumns.Select(r => $"r.`{r}` IS NULL")) + ";"
        );

        sql.Add(
            $"ALTER TABLE `{tableName}` ADD CONSTRAINT `{fkName}` " +
            $"FOREIGN KEY ({string.Join(", ", fk.Columns.Select(c => $"`{c}`"))}) " +
            $"REFERENCES `{fk.RefTable}` ({string.Join(", ", fk.RefColumns.Select(c => $"`{c}`"))}) " +
            $"ON DELETE {fk.OnDelete} ON UPDATE {fk.OnUpdate};"
        );
    }

    // -------------------------------------------------------
    // DROPS
    // -------------------------------------------------------

    private void EmitDrop(DiffOperation op, List<string> sql)
    {
        if (op.Description.StartsWith("Drop table"))
        {
            var table = ExtractName(op.Description);
            sql.Add($"DROP TABLE `{table}`;");
        }
        else if (op.Description.StartsWith("Drop column"))
        {
            var (table, column) = ExtractTableAndColumn(op.Description);
            sql.Add($"ALTER TABLE `{table}` DROP COLUMN `{column}`;");
        }
        else if (op.Description.StartsWith("Drop index"))
        {
            var (table, index) = ExtractTableAndIndex(op.Description);
            sql.Add($"DROP INDEX `{index}` ON `{table}`;");
        }
        else if (op.Description.StartsWith("Drop foreign key"))
        {
            var (table, fk) = ExtractTableAndConstraint(op.Description);
            sql.Add($"ALTER TABLE `{table}` DROP FOREIGN KEY `{fk}`;");
        }
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private static string RenderColumn(ColumnDef c)
    {
        var sb = new StringBuilder();
        sb.Append($"`{c.Name}` {c.ColumnType}");

        sb.Append(c.IsNullable ? " NULL" : " NOT NULL");

        if (c.Default != null)
            sb.Append($" DEFAULT {FormatDefault(c.Default, c.ColumnType)}");

        if (!string.IsNullOrWhiteSpace(c.Extra))
            sb.Append($" {c.Extra.ToUpperInvariant()}");

        return sb.ToString();
    }


    private static string FormatDefault(string value, string columnType)
    {
        // Normalize once
        var v = value.Trim();

        // CURRENT_TIMESTAMP and similar
        if (v.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase))
            return v;

        // BIT literal: b'0', b'1'
        if (IsBitType(columnType) && v.StartsWith("b'", StringComparison.OrdinalIgnoreCase))
            return v;

        // Numeric literal
        if (IsNumericLiteral(v))
            return v;

        // Everything else is a string literal
        return $"'{v.Replace("'", "''")}'";
    }

    private static bool IsBitType(string columnType)
    {
        return columnType.StartsWith("bit", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExtra(string extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
            return string.Empty;

        // Split into tokens, remove metadata-only flags
        var tokens = extra
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !t.Equals("DEFAULT_GENERATED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return string.Join(' ', tokens).ToLowerInvariant();
    }


    private static bool IsNumericLiteral(string value)
    {
        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out _);
    }



    // --- parsing helpers (based on your Description format) ---

    private static string ExtractTableName(string desc)
        => desc.Split(' ').Last();

    private static (string Table, string Column) ExtractTableAndColumn(string desc)
    {
        var parts = desc.Split(' ').Last().Split('.');
        return (parts[0], parts[1]);
    }

    private static (string Table, string Index) ExtractTableAndIndex(string desc)
    {
        var parts = desc.Split(' ').Last().Split('.');
        return (parts[0], parts[1]);
    }

    private static (string Table, string Constraint) ExtractTableAndConstraint(string desc)
    {
        var parts = desc.Split(' ').Last().Split('.');
        return (parts[0], parts[1]);
    }

    private static string ExtractName(string desc)
        => desc.Split(' ').Last();
}

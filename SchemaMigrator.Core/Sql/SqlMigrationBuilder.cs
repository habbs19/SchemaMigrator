using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;
using SchemaMigrator.Core.Sql;
using System.Text;

namespace SchemaMigrator.Core.Sql;

public sealed class SqlMigrationBuilder
{
    private readonly ISqlEmitter _emitter;

    public SqlMigrationBuilder(ISqlEmitter emitter)
    {
        _emitter = emitter;
    }

    public string Build(
        DiffPlan plan,
        SchemaSnapshot prod,
        SchemaSnapshot dev,
        EmitOptions options)
    {
        // Global safety gate
        if (plan.HasDestructiveOperations && !options.AllowDestructive)
            throw new InvalidOperationException(
                "Destructive operations detected. Enable AllowDestructive to proceed.");

        var result = _emitter.Emit(plan, prod, dev, options);

        var sb = new StringBuilder();

        // ---- Prechecks (commented by default) ----
        if (result.PrecheckSql.Any())
        {
            sb.AppendLine("-- =======================================");
            sb.AppendLine("-- PRECHECKS (run and verify before deploy)");
            sb.AppendLine("-- =======================================");

            foreach (var sql in result.PrecheckSql)
            {
                sb.AppendLine($"-- {sql}");
            }

            sb.AppendLine();
        }

        // ---- Migration SQL ----
        sb.AppendLine("-- ==================");
        sb.AppendLine("-- MIGRATION SQL");
        sb.AppendLine("-- ==================");

        foreach (var sql in result.MigrationSql)
        {
            sb.AppendLine(sql);
        }

        return sb.ToString();
    }
}

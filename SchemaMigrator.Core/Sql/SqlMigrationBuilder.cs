using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;
using System.Text;

namespace SchemaMigrator.Core.Sql;

/// <summary>
/// Builds formatted migration SQL output from a diff plan.
/// </summary>
public sealed class SqlMigrationBuilder
{
    private readonly ISqlEmitter _emitter;

    public SqlMigrationBuilder(ISqlEmitter emitter)
    {
        _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    }

    /// <summary>
    /// Build complete migration SQL with all sections.
    /// </summary>
    public MigrationOutput Build(
        DiffPlan plan,
        SchemaSnapshot source,
        SchemaSnapshot target,
        EmitOptions options)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        if (plan.HasDestructiveOperations && !options.AllowDestructive)
            throw new InvalidOperationException(
                "Destructive operations detected. Enable AllowDestructive to proceed.");

        var result = _emitter.Emit(plan, source, target, options);

        return new MigrationOutput
        {
            MigrationSql = FormatMigration(plan, result, options),
            PrecheckSql = FormatPrechecks(result),
            RollbackSql = options.GenerateRollback ? FormatRollback(result) : null,
            Summary = plan.Summary,
            Warnings = plan.Warnings.ToList()
        };
    }

    private static string FormatMigration(DiffPlan plan, SqlEmitResult result, EmitOptions options)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("-- ================================================");
        sb.AppendLine("-- SCHEMA MIGRATION");
        if (options.IncludeTimestamp)
            sb.AppendLine($"-- Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("-- ================================================");
        sb.AppendLine();

        // Summary
        var summary = plan.Summary;
        sb.AppendLine($"-- Operations: {summary.TotalOperations}");
        sb.AppendLine($"--   Safe: {summary.SafeOperations}");
        sb.AppendLine($"--   Risky: {summary.RiskyOperations}");
        sb.AppendLine($"--   Destructive: {summary.DestructiveOperations}");
        sb.AppendLine();

        // Warnings
        if (plan.Warnings.Any())
        {
            sb.AppendLine("-- ⚠️  WARNINGS:");
            foreach (var warning in plan.Warnings)
            {
                sb.AppendLine($"--   {warning}");
            }
            sb.AppendLine();
        }

        // Migration SQL
        foreach (var sql in result.MigrationSql)
        {
            sb.AppendLine(sql);
        }

        return sb.ToString();
    }

    private static string FormatPrechecks(SqlEmitResult result)
    {
        if (!result.PrecheckSql.Any())
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- ================================================");
        sb.AppendLine("-- PRECHECKS");
        sb.AppendLine("-- Run these queries BEFORE migration to validate data");
        sb.AppendLine("-- ================================================");
        sb.AppendLine();

        foreach (var sql in result.PrecheckSql)
        {
            sb.AppendLine(sql);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatRollback(SqlEmitResult result)
    {
        if (!result.RollbackSql.Any())
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("-- ================================================");
        sb.AppendLine("-- ROLLBACK");
        sb.AppendLine("-- Execute to reverse this migration");
        sb.AppendLine("-- ⚠️  Data added/modified cannot be restored");
        sb.AppendLine("-- ================================================");
        sb.AppendLine();

        foreach (var sql in result.RollbackSql)
        {
            sb.AppendLine(sql);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Complete migration output with all sections.
/// </summary>
public sealed class MigrationOutput
{
    public required string MigrationSql { get; init; }
    public required string PrecheckSql { get; init; }
    public string? RollbackSql { get; init; }
    public required DiffPlanSummary Summary { get; init; }
    public required IReadOnlyList<DiffWarning> Warnings { get; init; }

    /// <summary>
    /// Write all outputs to files.
    /// </summary>
    public void WriteToFiles(string basePath)
    {
        File.WriteAllText(Path.Combine(basePath, "migration.sql"), MigrationSql);

        if (!string.IsNullOrEmpty(PrecheckSql))
            File.WriteAllText(Path.Combine(basePath, "prechecks.sql"), PrecheckSql);

        if (!string.IsNullOrEmpty(RollbackSql))
            File.WriteAllText(Path.Combine(basePath, "rollback.sql"), RollbackSql);
    }
}

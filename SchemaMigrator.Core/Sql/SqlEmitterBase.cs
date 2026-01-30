using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;

namespace SchemaMigrator.Core.Sql;

public abstract class SqlEmitterBase : ISqlEmitter
{
    public SqlEmitResult Emit(
        DiffPlan plan,
        SchemaSnapshot prod,
        SchemaSnapshot dev,
        EmitOptions options)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (prod == null) throw new ArgumentNullException(nameof(prod));
        if (dev == null) throw new ArgumentNullException(nameof(dev));
        if (options == null) throw new ArgumentNullException(nameof(options));

        Validate(plan, options);

        var prechecks = new List<string>();
        var sql = new List<string>();

        if (options.UseTransaction)
            sql.Add("START TRANSACTION;");

        foreach (var phase in Enum.GetValues<SqlPhase>())
        {
            EmitPhase(
                phase,
                plan,
                prod,
                dev,
                options,
                prechecks,
                sql);
        }

        if (options.UseTransaction)
            sql.Add("COMMIT;");

        return new SqlEmitResult
        {
            PrecheckSql = prechecks,
            MigrationSql = sql
        };
    }

    /// <summary>
    /// Global validation before SQL emission.
    /// </summary>
    protected virtual void Validate(DiffPlan plan, EmitOptions options)
    {
        if (plan.HasDestructiveOperations && !options.AllowDestructive)
        {
            throw new InvalidOperationException(
                "Destructive operations detected. Set AllowDestructive=true to emit SQL.");
        }
    }

    /// <summary>
    /// Emit SQL for a specific phase.
    /// Implemented by database-specific emitters (e.g., MySQL).
    /// </summary>
    protected abstract void EmitPhase(
        SqlPhase phase,
        DiffPlan plan,
        SchemaSnapshot prod,
        SchemaSnapshot dev,
        EmitOptions options,
        List<string> prechecks,
        List<string> sql);
}

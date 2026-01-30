using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;

namespace SchemaMigrator.Core.Sql;

/// <summary>
/// Base class for database-specific SQL emitters.
/// </summary>
public abstract class SqlEmitterBase : ISqlEmitter
{
    public SqlEmitResult Emit(
        DiffPlan plan,
        SchemaSnapshot source,
        SchemaSnapshot target,
        EmitOptions options)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        Validate(plan, options);

        var context = new EmitContext(source, target, options);

        // Emit preparation phase
        EmitPreparation(context);

        // Emit each phase in order
        foreach (var phase in Enum.GetValues<SqlPhase>())
        {
            var ops = plan.Operations.Where(o => o.Phase == phase).ToList();
            
            foreach (var op in ops)
            {
                EmitOperation(op, context);
            }
        }

        // Emit cleanup phase
        EmitCleanup(context);

        return new SqlEmitResult
        {
            PrecheckSql = context.Prechecks,
            MigrationSql = context.Sql,
            RollbackSql = context.Rollback,
            Warnings = context.Warnings,
            IsComplete = context.SkippedOperations.Count == 0,
            SkippedOperations = context.SkippedOperations
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
    /// Emit preparation SQL (start transaction, disable FK checks, etc).
    /// </summary>
    protected abstract void EmitPreparation(EmitContext context);

    /// <summary>
    /// Emit cleanup SQL (commit, re-enable FK checks, etc).
    /// </summary>
    protected abstract void EmitCleanup(EmitContext context);

    /// <summary>
    /// Emit SQL for a single operation.
    /// </summary>
    protected abstract void EmitOperation(DiffOperation operation, EmitContext context);

    /// <summary>
    /// Context object carrying state through emission.
    /// </summary>
    protected sealed class EmitContext
    {
        public SchemaSnapshot Source { get; }
        public SchemaSnapshot Target { get; }
        public EmitOptions Options { get; }

        private readonly List<string> _prechecks = new();
        private readonly List<string> _sql = new();
        private readonly List<string> _rollback = new();
        private readonly List<string> _warnings = new();
        private readonly List<DiffOperation> _skipped = new();

        public IReadOnlyList<string> Prechecks => _prechecks;
        public IReadOnlyList<string> Sql => _sql;
        public IReadOnlyList<string> Rollback => _rollback;
        public IReadOnlyList<string> Warnings => _warnings;
        public IReadOnlyList<DiffOperation> SkippedOperations => _skipped;

        public EmitContext(SchemaSnapshot source, SchemaSnapshot target, EmitOptions options)
        {
            Source = source;
            Target = target;
            Options = options;
        }

        public void AddPrecheck(string sql) => _prechecks.Add(sql);
        public void AddSql(string sql) => _sql.Add(sql);
        public void AddRollback(string sql) => _rollback.Insert(0, sql); // Reverse order
        public void AddWarning(string warning) => _warnings.Add(warning);
        public void Skip(DiffOperation op) => _skipped.Add(op);

        public void AddComment(string comment)
        {
            if (Options.IncludeComments)
                _sql.Add($"-- {comment}");
        }
    }
}

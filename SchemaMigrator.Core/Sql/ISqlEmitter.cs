using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;

namespace SchemaMigrator.Core.Sql;

/// <summary>
/// Generates SQL statements from a diff plan.
/// </summary>
public interface ISqlEmitter
{
    /// <summary>
    /// Emits SQL statements required to apply the diff plan.
    /// </summary>
    /// <param name="plan">The diff plan describing schema changes.</param>
    /// <param name="source">Current (source) schema snapshot.</param>
    /// <param name="target">Target (desired) schema snapshot.</param>
    /// <param name="options">SQL emission options.</param>
    /// <returns>A result containing precheck SQL and migration SQL.</returns>
    SqlEmitResult Emit(
        DiffPlan plan,
        SchemaSnapshot source,
        SchemaSnapshot target,
        EmitOptions options);
}

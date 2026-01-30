using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Models;
using SchemaMigrator.Core.Options;

namespace SchemaMigrator.Core.Sql;

public interface ISqlEmitter
{
    /// <summary>
    /// Emits SQL statements required to apply the diff plan.
    /// </summary>
    /// <param name="plan">The diff plan describing schema changes.</param>
    /// <param name="prod">Current production schema snapshot.</param>
    /// <param name="dev">Target (development) schema snapshot.</param>
    /// <param name="options">SQL emission options.</param>
    /// <returns>
    /// A result containing precheck SQL and migration SQL.
    /// </returns>
    SqlEmitResult Emit(
        DiffPlan plan,
        SchemaSnapshot prod,
        SchemaSnapshot dev,
        EmitOptions options);
}

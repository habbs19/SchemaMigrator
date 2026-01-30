using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaMigrator.Core.Options;

public sealed class EmitOptions
{
    /// <summary>
    /// Allow emitting destructive SQL (DROP TABLE, DROP COLUMN, etc).
    /// </summary>
    public bool AllowDestructive { get; init; } = false;
    public bool UseTransaction { get; init; } = true;
}

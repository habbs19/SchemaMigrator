using SchemaMigrator.Core.Sql;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaMigrator.Core.Diff;

public sealed record DiffOperation(
    SqlPhase Phase,
    string Description,
    bool IsDestructive,
    bool IsRisky
);

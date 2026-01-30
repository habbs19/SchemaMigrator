using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaMigrator.Core.Sql;

public enum SqlPhase
{
    CreateTables = 1,
    AddColumns = 2,
    Backfill = 3,
    AddIndexes = 4,
    AddForeignKeys = 5,
    ModifyColumns = 6,
    Drops = 7
}

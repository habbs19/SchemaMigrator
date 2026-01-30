using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaMigrator.Core.Models;

public sealed class TableDef
{
    public required string Name { get; init; }

    public Dictionary<string, ColumnDef> Columns { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, IndexDef> Indexes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ForeignKeyDef> ForeignKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IndexDef? PrimaryKey { get; set; }
}

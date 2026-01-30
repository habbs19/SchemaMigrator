using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaMigrator.Core.Models;

public sealed class ColumnDef
{
    public required string Name { get; init; }

    public required string ColumnType { get; init; }      // e.g. "bigint unsigned", "varchar(100)"
    public bool IsNullable { get; init; }
    public string? Default { get; init; }                 // normalized
    public string Extra { get; init; } = "";               // auto_increment, on update, etc.
    public string? Charset { get; init; }
    public string? Collation { get; init; }
}

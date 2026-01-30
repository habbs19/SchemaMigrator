using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaMigrator.Core.Models;

public sealed class SchemaSnapshot
{
    public Dictionary<string, TableDef> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);
}
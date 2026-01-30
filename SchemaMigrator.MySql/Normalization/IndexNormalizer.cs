using SchemaMigrator.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaMigrator.MySql.Normalization;

public static class IndexNormalizer
{
    public static IReadOnlyDictionary<string, IndexDef> Normalize(IEnumerable<IndexRow> rows)
    {
        return rows
            .GroupBy(r => r.INDEX_NAME, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => BuildIndex(g),
                StringComparer.OrdinalIgnoreCase
            );
    }

    private static IndexDef BuildIndex(IEnumerable<IndexRow> rows)
    {
        var ordered = rows
            .OrderBy(r => r.SEQ_IN_INDEX)
            .ToList();

        var first = ordered[0];

        return new IndexDef
        {
            Name = first.INDEX_NAME,
            IsUnique = first.NON_UNIQUE == 0,
            IndexType = NormalizeIndexType(first.INDEX_TYPE),
            Columns = ordered.Select(r => r.COLUMN_NAME).ToList()
        };
    }

    private static string NormalizeIndexType(string indexType)
    {
        return indexType
            .Trim()
            .ToUpperInvariant();
    }
}

using SchemaMigrator.Core.Models;

namespace SchemaMigrator.MySql.Normalization;

/// <summary>
/// Normalizes index data from MySQL information_schema to consistent IndexDef format.
/// </summary>
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
            Columns = ordered.Select(r => r.COLUMN_NAME).ToList(),
            ColumnDirections = ordered.Select(r => NormalizeDirection(r.COLUMN_DIRECTION)).ToList(),
            PrefixLengths = ordered.Select(r => r.PREFIX_LENGTH).ToList(),
            Comment = string.IsNullOrWhiteSpace(first.INDEX_COMMENT) ? null : first.INDEX_COMMENT,
            IsVisible = NormalizeVisibility(first.IS_VISIBLE)
        };
    }

    private static string NormalizeIndexType(string indexType)
    {
        return indexType.Trim().ToUpperInvariant();
    }

    private static string NormalizeDirection(string? direction)
    {
        // A = ASC, D = DESC, null = not sorted
        return direction?.ToUpperInvariant() switch
        {
            "A" => "ASC",
            "D" => "DESC",
            _ => "ASC"
        };
    }

    private static bool NormalizeVisibility(string? isVisible)
    {
        // MySQL 8+: YES/NO, older versions: null
        return !string.Equals(isVisible, "NO", StringComparison.OrdinalIgnoreCase);
    }
}

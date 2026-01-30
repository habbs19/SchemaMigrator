using SchemaMigrator.Core.Models;

namespace SchemaMigrator.MySql.Normalization;

/// <summary>
/// Normalizes foreign key data from MySQL information_schema to consistent ForeignKeyDef format.
/// </summary>
public static class ForeignKeyNormalizer
{
    public static IReadOnlyDictionary<string, ForeignKeyDef> Normalize(IEnumerable<ForeignKeyRow> rows)
    {
        return rows
            .GroupBy(r => r.CONSTRAINT_NAME, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => BuildForeignKey(g),
                StringComparer.OrdinalIgnoreCase
            );
    }

    private static ForeignKeyDef BuildForeignKey(IEnumerable<ForeignKeyRow> rows)
    {
        var ordered = rows
            .OrderBy(r => r.ORDINAL_POSITION)
            .ToList();

        var first = ordered[0];

        return new ForeignKeyDef
        {
            Name = first.CONSTRAINT_NAME,
            Table = first.TABLE_NAME,
            Columns = ordered.Select(r => r.COLUMN_NAME).ToList(),
            RefTable = first.REFERENCED_TABLE_NAME,
            RefColumns = ordered.Select(r => r.REFERENCED_COLUMN_NAME).ToList(),
            OnDelete = NormalizeRule(first.DELETE_RULE),
            OnUpdate = NormalizeRule(first.UPDATE_RULE)
        };
    }

    private static string NormalizeRule(string rule)
    {
        return rule.Trim().ToUpperInvariant();
    }
}

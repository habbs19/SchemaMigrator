using SchemaMigrator.Core.Models;

namespace SchemaMigrator.MySql.Normalization;

/// <summary>
/// Normalizes column data from MySQL information_schema to consistent ColumnDef format.
/// </summary>
public static class ColumnNormalizer
{
    public static ColumnDef Normalize(ColumnRow c)
    {
        return new ColumnDef
        {
            Name = c.COLUMN_NAME,
            ColumnType = NormalizeColumnType(c.COLUMN_TYPE),
            IsNullable = NormalizeNullable(c.IS_NULLABLE),
            Default = NormalizeDefault(c.COLUMN_DEFAULT),
            Extra = NormalizeExtra(c.EXTRA),
            Charset = c.CHARACTER_SET_NAME,
            Collation = c.COLLATION_NAME,
            Comment = string.IsNullOrWhiteSpace(c.COLUMN_COMMENT) ? null : c.COLUMN_COMMENT,
            OrdinalPosition = c.ORDINAL_POSITION
        };
    }

    private static string NormalizeColumnType(string columnType)
    {
        // MySQL may return weird spacing/casing
        return columnType
            .Trim()
            .ToLowerInvariant()
            .Replace("  ", " ");
    }

    private static bool NormalizeNullable(string isNullable)
    {
        return isNullable.Equals("YES", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeDefault(string? columnDefault)
    {
        if (columnDefault == null)
            return null;

        var value = columnDefault.Trim();

        // Empty string is still a valid default
        if (value.Length == 0)
            return "";

        // Normalize CURRENT_TIMESTAMP variants
        if (value.Equals("current_timestamp()", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("current_timestamp", StringComparison.OrdinalIgnoreCase))
        {
            return "CURRENT_TIMESTAMP";
        }

        // Handle CURRENT_TIMESTAMP with precision
        if (value.StartsWith("current_timestamp(", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToUpperInvariant();
        }

        // Normalize NOW() variant
        if (value.Equals("now()", StringComparison.OrdinalIgnoreCase))
        {
            return "CURRENT_TIMESTAMP";
        }

        // Normalize UUID()
        if (value.StartsWith("uuid(", StringComparison.OrdinalIgnoreCase))
        {
            return value.ToLowerInvariant();
        }

        // Strip surrounding single quotes for string literals
        if (value.Length >= 2 &&
            value.StartsWith('\'') &&
            value.EndsWith('\''))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string NormalizeExtra(string extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
            return string.Empty;

        // Split into tokens, remove metadata-only flags that don't affect behavior
        var tokens = extra
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !t.Equals("DEFAULT_GENERATED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return string.Join(' ', tokens).ToLowerInvariant();
    }
}

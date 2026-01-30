using SchemaMigrator.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaMigrator.MySql.Normalization;

public static class ColumnNormalizer
{
    public static ColumnDef Normalize(Columns c)
    {
        return new ColumnDef
        {
            Name = c.COLUMN_NAME,
            ColumnType = NormalizeColumnType(c.COLUMN_TYPE),
            IsNullable = NormalizeNullable(c.IS_NULLABLE),
            Default = NormalizeDefault(c.COLUMN_DEFAULT),
            Extra = NormalizeExtra(c.EXTRA),
            Charset = c.CHARACTER_SET_NAME,
            Collation = c.COLLATION_NAME
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

        // Normalize CURRENT_TIMESTAMP variants
        if (value.Equals("current_timestamp()", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("current_timestamp", StringComparison.OrdinalIgnoreCase))
        {
            return "CURRENT_TIMESTAMP";
        }

        // Strip surrounding quotes
        if (value.Length >= 2 &&
            value.StartsWith("'") &&
            value.EndsWith("'"))
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }

    private static string NormalizeExtra(string extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
            return string.Empty;

        // Split into tokens, remove metadata-only flags
        var tokens = extra
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !t.Equals("DEFAULT_GENERATED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return string.Join(' ', tokens).ToLowerInvariant();
    }
}

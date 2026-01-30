using System.Text.Json.Serialization;

namespace SchemaMigrator.MySql;

/// <summary>
/// Row from information_schema.columns.
/// </summary>
public sealed class ColumnRow
{
    [JsonPropertyName("TABLE_NAME")]
    public string TABLE_NAME { get; init; } = "";

    [JsonPropertyName("COLUMN_NAME")]
    public string COLUMN_NAME { get; init; } = "";

    [JsonPropertyName("ORDINAL_POSITION")]
    public int ORDINAL_POSITION { get; init; }

    [JsonPropertyName("COLUMN_TYPE")]
    public string COLUMN_TYPE { get; init; } = "";

    [JsonPropertyName("IS_NULLABLE")]
    public string IS_NULLABLE { get; init; } = "";

    [JsonPropertyName("COLUMN_DEFAULT")]
    public string? COLUMN_DEFAULT { get; init; }

    [JsonPropertyName("EXTRA")]
    public string EXTRA { get; init; } = "";

    [JsonPropertyName("CHARACTER_SET_NAME")]
    public string? CHARACTER_SET_NAME { get; init; }

    [JsonPropertyName("COLLATION_NAME")]
    public string? COLLATION_NAME { get; init; }

    [JsonPropertyName("COLUMN_COMMENT")]
    public string? COLUMN_COMMENT { get; init; }
}

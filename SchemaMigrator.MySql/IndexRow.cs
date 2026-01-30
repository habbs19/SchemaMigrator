using System.Text.Json.Serialization;

namespace SchemaMigrator.MySql;

/// <summary>
/// Row from information_schema.statistics.
/// </summary>
public sealed class IndexRow
{
    [JsonPropertyName("TABLE_NAME")]
    public string TABLE_NAME { get; init; } = "";

    [JsonPropertyName("INDEX_NAME")]
    public string INDEX_NAME { get; init; } = "";

    [JsonPropertyName("NON_UNIQUE")]
    public int NON_UNIQUE { get; init; }  // 0 = unique, 1 = non-unique

    [JsonPropertyName("SEQ_IN_INDEX")]
    public int SEQ_IN_INDEX { get; init; }

    [JsonPropertyName("COLUMN_NAME")]
    public string COLUMN_NAME { get; init; } = "";

    [JsonPropertyName("INDEX_TYPE")]
    public string INDEX_TYPE { get; init; } = "";

    [JsonPropertyName("COLUMN_DIRECTION")]
    public string? COLUMN_DIRECTION { get; init; }  // A = ASC, D = DESC, NULL = not sorted

    [JsonPropertyName("PREFIX_LENGTH")]
    public int? PREFIX_LENGTH { get; init; }  // sub_part - prefix length for partial indexes

    [JsonPropertyName("INDEX_COMMENT")]
    public string? INDEX_COMMENT { get; init; }

    [JsonPropertyName("IS_VISIBLE")]
    public string? IS_VISIBLE { get; init; }  // YES/NO (MySQL 8+)
}

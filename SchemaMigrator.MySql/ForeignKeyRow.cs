using System.Text.Json.Serialization;

namespace SchemaMigrator.MySql;

/// <summary>
/// Row from information_schema.key_column_usage joined with referential_constraints.
/// </summary>
public sealed record ForeignKeyRow
{
    [JsonPropertyName("TABLE_NAME")]
    public string TABLE_NAME { get; init; } = "";

    [JsonPropertyName("CONSTRAINT_NAME")]
    public string CONSTRAINT_NAME { get; init; } = "";

    [JsonPropertyName("COLUMN_NAME")]
    public string COLUMN_NAME { get; init; } = "";

    [JsonPropertyName("ORDINAL_POSITION")]
    public int ORDINAL_POSITION { get; init; }

    [JsonPropertyName("REFERENCED_TABLE_NAME")]
    public string REFERENCED_TABLE_NAME { get; init; } = "";

    [JsonPropertyName("REFERENCED_COLUMN_NAME")]
    public string REFERENCED_COLUMN_NAME { get; init; } = "";

    [JsonPropertyName("UPDATE_RULE")]
    public string UPDATE_RULE { get; init; } = "";

    [JsonPropertyName("DELETE_RULE")]
    public string DELETE_RULE { get; init; } = "";
}

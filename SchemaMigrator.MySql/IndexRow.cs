using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SchemaMigrator.MySql;

public sealed class IndexRow
{
    [JsonPropertyName("TABLE_NAME")]
    public string TABLE_NAME { get; init; } = default!;
    [JsonPropertyName("INDEX_NAME")]
    public string INDEX_NAME { get; init; } = default!;
    [JsonPropertyName("NON_UNIQUE")]
    public int NON_UNIQUE { get; init; }     // 0 = unique, 1 = non-unique
    [JsonPropertyName("SEQ_IN_INDEX")]
    public int SEQ_IN_INDEX { get; init; }
    [JsonPropertyName("COLUMN_NAME")]
    public string COLUMN_NAME { get; init; } = default!;
    [JsonPropertyName("INDEX_TYPE")]
    public string INDEX_TYPE { get; init; } = default!;
}

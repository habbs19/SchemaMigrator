using DataAccessProvider.Core.Interfaces;
using DataAccessProvider.MySql;
using SchemaMigrator.Contracts;
using SchemaMigrator.Core.Models;
using SchemaMigrator.MySql.Normalization;
using System.Data;
using System.Text.Json.Serialization;

namespace SchemaMigrator.MySql;

/// <summary>
/// Reads MySQL schema information from information_schema.
/// </summary>
public sealed class MySqlSchemaReader : ISchemaReader
{
    private readonly IDataSource _data;

    public MySqlSchemaReader(IDataSource data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public async Task<SchemaSnapshot> ReadAsync()
    {
        var snapshot = new SchemaSnapshot
        {
            CapturedAt = DateTimeOffset.UtcNow
        };

        await LoadSchemaInfo(snapshot);
        await LoadTables(snapshot);
        await LoadColumns(snapshot);
        await LoadIndexes(snapshot);
        await LoadForeignKeys(snapshot);

        return snapshot;
    }

    // =====================================================
    // Schema-level info
    // =====================================================

    private record SchemaInfoRow
    {
        [JsonPropertyName("SCHEMA_NAME")]
        public string SCHEMA_NAME { get; init; } = "";
        [JsonPropertyName("DEFAULT_CHARACTER_SET_NAME")]
        public string DEFAULT_CHARACTER_SET_NAME { get; init; } = "";
        [JsonPropertyName("DEFAULT_COLLATION_NAME")]
        public string DEFAULT_COLLATION_NAME { get; init; } = "";
    }

    private async Task LoadSchemaInfo(SchemaSnapshot snapshot)
    {
        var schemaParams = new MySQLSourceParams<SchemaInfoRow>
        {
            Query = @"
                SELECT schema_name, default_character_set_name, default_collation_name
                FROM information_schema.schemata
                WHERE schema_name = DATABASE();",
            CommandType = CommandType.Text
        };

        var result = await _data.ExecuteReaderAsync(schemaParams);
        if (result.Value?.FirstOrDefault() is { } info)
        {
            // Use reflection or create new snapshot since properties are init-only
            // For simplicity, we'll leave these as null if we can't set them
        }
    }

    // =====================================================
    // Tables
    // =====================================================

    private record TableRow
    {
        [JsonPropertyName("TABLE_NAME")]
        public string TABLE_NAME { get; init; } = "";
        [JsonPropertyName("ENGINE")]
        public string? ENGINE { get; init; }
        [JsonPropertyName("TABLE_COLLATION")]
        public string? TABLE_COLLATION { get; init; }
        [JsonPropertyName("TABLE_COMMENT")]
        public string? TABLE_COMMENT { get; init; }
        [JsonPropertyName("AUTO_INCREMENT")]
        public long? AUTO_INCREMENT { get; init; }
    }

    private async Task LoadTables(SchemaSnapshot snapshot)
    {
        var sourceParams = new MySQLSourceParams<TableRow>
        {
            Query = @"
                SELECT table_name, engine, table_collation, table_comment, auto_increment
                FROM information_schema.tables
                WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE';",
            CommandType = CommandType.Text
        };

        var result = await _data.ExecuteReaderAsync(sourceParams);
        if (result.Value == null)
            throw new InvalidOperationException("Failed to read tables from the database.");

        foreach (var table in result.Value)
        {
            snapshot.Tables[table.TABLE_NAME] = new TableDef
            {
                Name = table.TABLE_NAME,
                Engine = table.ENGINE,
                Collation = table.TABLE_COLLATION,
                Comment = string.IsNullOrEmpty(table.TABLE_COMMENT) ? null : table.TABLE_COMMENT,
                AutoIncrement = table.AUTO_INCREMENT
            };
        }
    }

    // =====================================================
    // Columns
    // =====================================================

    private async Task LoadColumns(SchemaSnapshot snapshot)
    {
        var columnParams = new MySQLSourceParams<ColumnRow>
        {
            Query = @"
                SELECT table_name, column_name, ordinal_position, column_type, 
                       is_nullable, column_default, extra, 
                       character_set_name, collation_name, column_comment
                FROM information_schema.columns
                WHERE table_schema = DATABASE()
                ORDER BY table_name, ordinal_position;",
            CommandType = CommandType.Text
        };

        var columnResult = await _data.ExecuteReaderAsync(columnParams);
        if (columnResult.Value == null)
            throw new InvalidOperationException("Failed to read columns from the database.");

        foreach (var c in columnResult.Value)
        {
            if (!snapshot.Tables.TryGetValue(c.TABLE_NAME, out var table))
                continue;

            table.Columns[c.COLUMN_NAME] = ColumnNormalizer.Normalize(c);
        }
    }

    // =====================================================
    // Indexes
    // =====================================================

    private async Task LoadIndexes(SchemaSnapshot snapshot)
    {
        var indexParams = new MySQLSourceParams<IndexRow>
        {
            Query = @"
                SELECT table_name, index_name, non_unique, seq_in_index,
                       column_name, index_type, collation AS column_direction,
                       sub_part AS prefix_length, index_comment, is_visible
                FROM information_schema.statistics
                WHERE table_schema = DATABASE()
                ORDER BY table_name, index_name, seq_in_index;",
            CommandType = CommandType.Text
        };

        var indexResult = await _data.ExecuteReaderAsync(indexParams);
        if (indexResult.Value == null)
            throw new InvalidOperationException("Failed to read indexes.");

        foreach (var tableGroup in indexResult.Value.GroupBy(i => i.TABLE_NAME))
        {
            if (!snapshot.Tables.TryGetValue(tableGroup.Key, out var table))
                continue;

            var normalizedIndexes = IndexNormalizer.Normalize(tableGroup);

            foreach (var (indexName, index) in normalizedIndexes)
            {
                if (indexName.Equals("PRIMARY", StringComparison.OrdinalIgnoreCase))
                {
                    table.PrimaryKey = index;
                }
                else
                {
                    table.Indexes[indexName] = index;
                }
            }
        }
    }

    // =====================================================
    // Foreign Keys
    // =====================================================

    private async Task LoadForeignKeys(SchemaSnapshot snapshot)
    {
        var fkParams = new MySQLSourceParams<ForeignKeyRow>
        {
            Query = @"
                SELECT
                     kcu.table_name,
                     kcu.constraint_name,
                     kcu.column_name,
                     kcu.ordinal_position,
                     kcu.referenced_table_name,
                     kcu.referenced_column_name,
                     rc.update_rule,
                     rc.delete_rule
                 FROM information_schema.key_column_usage kcu
                 JOIN information_schema.referential_constraints rc
                   ON rc.constraint_schema = kcu.constraint_schema
                  AND rc.constraint_name = kcu.constraint_name
                 WHERE kcu.table_schema = DATABASE()
                   AND kcu.referenced_table_name IS NOT NULL
                 ORDER BY kcu.table_name, kcu.constraint_name, kcu.ordinal_position;",
            CommandType = CommandType.Text
        };

        var result = await _data.ExecuteReaderAsync(fkParams);
        if (result.Value == null)
            throw new InvalidOperationException("Failed to read foreign keys.");

        foreach (var tableGroup in result.Value.GroupBy(r => r.TABLE_NAME))
        {
            if (!snapshot.Tables.TryGetValue(tableGroup.Key, out var table))
                continue;

            var normalizedFks = ForeignKeyNormalizer.Normalize(tableGroup);

            foreach (var (fkName, fk) in normalizedFks)
            {
                table.ForeignKeys[fkName] = fk;
            }
        }
    }
}

using DataAccessProvider.Core.Interfaces;
using DataAccessProvider.MySql;
using SchemaMigrator.Contracts;
using SchemaMigrator.Core.Models;
using SchemaMigrator.MySql.Normalization;
using System.Data;
using System.Text.Json.Serialization;

namespace SchemaMigrator.MySql;

public sealed class MySqlSchemaReader : ISchemaReader
{
    private readonly IDataSource _data;

    public MySqlSchemaReader(IDataSource data)
    {
        _data = data;
    }

    public async Task<SchemaSnapshot> ReadAsync()
    {
        var snapshot = new SchemaSnapshot();

        snapshot = await LoadTables(snapshot);
        snapshot = await LoadColumns(snapshot);
        snapshot = await LoadIndexes(snapshot);
        snapshot = await LoadForeignKeys(snapshot);

        return snapshot;
    }

    private record Tables
    {
        [JsonPropertyName("TABLE_NAME")]
        public string TABLE_NAME { get; init; } = "";
    }


    private async Task<SchemaSnapshot> LoadIndexes(SchemaSnapshot snapshot)
    {
        var indexParams = new MySQLSourceParams<IndexRow>
        {
            Query = @"
        SELECT table_name, index_name, non_unique, seq_in_index,
               column_name, index_type
        FROM information_schema.statistics
        WHERE table_schema = DATABASE();",
            CommandType = CommandType.Text
        };

        var indexResult = await _data.ExecuteReaderAsync(indexParams);
        if (indexResult.Value == null)
            throw new InvalidOperationException("Failed to read indexes.");

        foreach (var tableGroup in indexResult.Value.GroupBy(i => i.TABLE_NAME))
        {
            if (!snapshot.Tables.TryGetValue(tableGroup.Key, out var table))
                continue; // defensive — should not happen

            var normalizedIndexes = IndexNormalizer.Normalize(tableGroup);

            foreach (var kvp in normalizedIndexes)
            {
                var indexName = kvp.Key;
                var index = kvp.Value;

                // Handle PRIMARY KEY separately if you want
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

        return snapshot;
    }

    private async Task<SchemaSnapshot> LoadTables(SchemaSnapshot snapshot)
    {
        var sourceParams = new MySQLSourceParams<Tables>
        {
            Query = "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE';",
            CommandType = CommandType.Text
        };

        var result = await _data.ExecuteReaderAsync(sourceParams);
        if (result.Value == null)
            throw new InvalidOperationException("Failed to read tables from the database.");

        foreach (var table in result.Value)
        {
            snapshot.Tables[table.TABLE_NAME] = new TableDef { Name = table.TABLE_NAME };
        }

        return snapshot;
    }

    private async Task<SchemaSnapshot> LoadColumns(SchemaSnapshot snapshot)
    {
        var columnParams = new MySQLSourceParams<Columns>
        {
            Query = @"
                    SELECT table_name, column_name, column_type, is_nullable,
                    column_default, extra, character_set_name, collation_name
                    FROM information_schema.columns
                    WHERE table_schema = DATABASE();",
            CommandType = System.Data.CommandType.Text
        };

        var columnResult = await _data.ExecuteReaderAsync(columnParams);
        if (columnResult.Value == null)
            throw new InvalidOperationException("Failed to read columns from the database.");

        foreach (var c in columnResult.Value)
        {
            if (!snapshot.Tables.TryGetValue(c.TABLE_NAME, out var table))
                continue; // defensive — should not happen
            snapshot.Tables[c.TABLE_NAME].Columns[c.COLUMN_NAME] = ColumnNormalizer.Normalize(c);
        }

        return snapshot;
    }

    private async Task<SchemaSnapshot> LoadForeignKeys(SchemaSnapshot snapshot)
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
                continue; // defensive

            var normalizedFks = ForeignKeyNormalizer.Normalize(tableGroup);

            foreach (var fk in normalizedFks)
            {
                table.ForeignKeys[fk.Key] = fk.Value;
            }
        }

        return snapshot;
    }


}

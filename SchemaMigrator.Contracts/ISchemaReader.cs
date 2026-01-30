using SchemaMigrator.Core.Models;

namespace SchemaMigrator.Contracts;

/// <summary>
/// Reads database schema information into a snapshot.
/// </summary>
public interface ISchemaReader
{
    /// <summary>
    /// Reads the complete schema from the database.
    /// </summary>
    Task<SchemaSnapshot> ReadAsync();
}

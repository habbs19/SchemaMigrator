using SchemaMigrator.Core.Models;
namespace SchemaMigrator.Contracts;

public interface ISchemaReader
{
    Task<SchemaSnapshot> ReadAsync();
}

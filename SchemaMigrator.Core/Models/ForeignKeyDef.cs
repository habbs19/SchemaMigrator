namespace SchemaMigrator.Core.Models;

public sealed class ForeignKeyDef
{
    public required string Name { get; init; }
    public required string Table { get; init; }
    public required IReadOnlyList<string> Columns { get; init; }
    public required string RefTable { get; init; }
    public required IReadOnlyList<string> RefColumns { get; init; }
    public required string OnDelete { get; init; }
    public required string OnUpdate { get; init; }
}

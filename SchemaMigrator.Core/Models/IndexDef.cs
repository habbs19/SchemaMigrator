namespace SchemaMigrator.Core.Models;

public sealed class IndexDef
{
    public required string Name { get; init; }
    public bool IsUnique { get; init; }
    public required string IndexType { get; init; }        // BTREE, HASH
    public IReadOnlyList<string> Columns { get; init; } = [];
}

namespace SchemaMigrator.Core.Diff;

public sealed class DiffWarning
{
    public required DiffWarningType Type { get; init; }

    public required string Message { get; init; }

    public string? Table { get; init; }

    public string? ObjectName { get; init; }

    public override string ToString()
    {
        return Table == null
            ? $"{Type}: {Message}"
            : $"{Type}: {Table}.{ObjectName} — {Message}";
    }
}

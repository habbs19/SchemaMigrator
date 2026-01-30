namespace SchemaMigrator.Core.Diff;

/// <summary>
/// Represents a warning about a potentially problematic schema change.
/// </summary>
public sealed class DiffWarning
{
    public required DiffWarningType Type { get; init; }
    public required string Message { get; init; }
    public string? Table { get; init; }
    public string? ObjectName { get; init; }
    
    /// <summary>
    /// Suggested action to mitigate the risk.
    /// </summary>
    public string? Mitigation { get; init; }

    public override string ToString()
    {
        var location = Table == null 
            ? "" 
            : ObjectName == null 
                ? $"{Table}: " 
                : $"{Table}.{ObjectName}: ";
        
        return $"[{Type}] {location}{Message}";
    }
}

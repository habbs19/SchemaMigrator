namespace SchemaMigrator.Core.Diff;

public sealed class DiffPlan
{
    private readonly List<DiffOperation> _operations = new();
    private readonly List<DiffWarning> _warnings = new();

    public IReadOnlyList<DiffOperation> Operations => _operations;
    public IReadOnlyList<DiffWarning> Warnings => _warnings;

    public bool HasDestructiveOperations =>
        _operations.Any(o => o.IsDestructive);

    public bool HasRiskyOperations =>
        _operations.Any(o => o.IsRisky);

    public void AddOperation(DiffOperation operation)
    {
        _operations.Add(operation);
    }

    public void AddWarning(DiffWarning warning)
    {
        _warnings.Add(warning);
    }
}

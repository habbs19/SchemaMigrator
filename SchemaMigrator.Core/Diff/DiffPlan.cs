namespace SchemaMigrator.Core.Diff;

/// <summary>
/// Represents a complete migration plan with operations and warnings.
/// </summary>
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

    public bool IsEmpty => _operations.Count == 0;

    /// <summary>
    /// Gets a summary of operations by type.
    /// </summary>
    public DiffPlanSummary Summary => new(this);

    public void AddOperation(DiffOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _operations.Add(operation);
    }

    public void AddWarning(DiffWarning warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        _warnings.Add(warning);
    }

    /// <summary>
    /// Gets all operations of a specific type.
    /// </summary>
    public IEnumerable<DiffOperation> GetOperations(DiffOperationType type) =>
        _operations.Where(o => o.OperationType == type);

    /// <summary>
    /// Gets all operations for a specific table.
    /// </summary>
    public IEnumerable<DiffOperation> GetOperationsForTable(string tableName) =>
        _operations.Where(o => o.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Provides summary statistics for a diff plan.
/// </summary>
public sealed class DiffPlanSummary
{
    private readonly DiffPlan _plan;

    internal DiffPlanSummary(DiffPlan plan) => _plan = plan;

    public int TotalOperations => _plan.Operations.Count;
    public int DestructiveOperations => _plan.Operations.Count(o => o.IsDestructive);
    public int RiskyOperations => _plan.Operations.Count(o => o.IsRisky);
    public int SafeOperations => _plan.Operations.Count(o => !o.IsDestructive && !o.IsRisky);
    public int Warnings => _plan.Warnings.Count;

    public int TablesCreated => _plan.Operations.Count(o => o.OperationType == DiffOperationType.CreateTable);
    public int TablesDropped => _plan.Operations.Count(o => o.OperationType == DiffOperationType.DropTable);
    public int ColumnsAdded => _plan.Operations.Count(o => o.OperationType == DiffOperationType.AddColumn);
    public int ColumnsDropped => _plan.Operations.Count(o => o.OperationType == DiffOperationType.DropColumn);
    public int ColumnsModified => _plan.Operations.Count(o => o.OperationType == DiffOperationType.ModifyColumn);
    public int IndexesAdded => _plan.Operations.Count(o => o.OperationType == DiffOperationType.AddIndex);
    public int IndexesDropped => _plan.Operations.Count(o => o.OperationType == DiffOperationType.DropIndex);
    public int ForeignKeysAdded => _plan.Operations.Count(o => o.OperationType == DiffOperationType.AddForeignKey);
    public int ForeignKeysDropped => _plan.Operations.Count(o => o.OperationType == DiffOperationType.DropForeignKey);

    public override string ToString() =>
        $"Operations: {TotalOperations} (Safe: {SafeOperations}, Risky: {RiskyOperations}, Destructive: {DestructiveOperations}), Warnings: {Warnings}";
}

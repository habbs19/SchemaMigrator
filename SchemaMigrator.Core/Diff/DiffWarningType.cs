namespace SchemaMigrator.Core.Diff;

public enum DiffWarningType
{
    RiskyChange,
    DestructiveChange,
    RequiresManualReview,
    PotentialDataLoss,
    UnsupportedChange
}

namespace SchemaMigrator.Core.Diff;

public enum DiffWarningType
{
    /// <summary>Change may cause issues but is recoverable.</summary>
    RiskyChange,

    /// <summary>Change will permanently delete data.</summary>
    DestructiveChange,

    /// <summary>Change requires manual review before execution.</summary>
    RequiresManualReview,

    /// <summary>Change may cause data loss or truncation.</summary>
    PotentialDataLoss,

    /// <summary>Change type is not supported by automated migration.</summary>
    UnsupportedChange,

    /// <summary>Change may impact query performance.</summary>
    PerformanceImpact,

    /// <summary>Change may violate data integrity constraints.</summary>
    IntegrityRisk
}

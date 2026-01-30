using SchemaMigrator.Core.Sql;

namespace SchemaMigrator.Core.Diff;

/// <summary>
/// Represents a single schema change operation with strongly-typed metadata.
/// </summary>
public sealed record DiffOperation(
    SqlPhase Phase,
    DiffOperationType OperationType,
    string TableName,
    string? ObjectName,
    string Description,
    bool IsDestructive,
    bool IsRisky
);

/// <summary>
/// Categorizes the type of diff operation for type-safe handling.
/// </summary>
public enum DiffOperationType
{
    // Table operations
    CreateTable,
    DropTable,
    
    // Column operations
    AddColumn,
    DropColumn,
    ModifyColumn,
    
    // Index operations
    AddIndex,
    DropIndex,
    
    // Foreign key operations
    AddForeignKey,
    DropForeignKey,
    
    // Primary key operations
    AddPrimaryKey,
    DropPrimaryKey,
    ModifyPrimaryKey
}

using DataAccessProvider.Core.Interfaces;
using DataAccessProvider.MySql;
using Microsoft.Extensions.DependencyInjection;
using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Options;
using SchemaMigrator.Core.Sql;
using SchemaMigrator.MySql;

// =====================================================
// CONFIGURATION (could come from command line args or config file)
// =====================================================

var config = new MigrationConfig
{
    // Source = current state (e.g., production)
    SourceConnectionString = "Server=127.0.0.1;Port=3306;Database=aznv;Uid=root;Pwd=password;",
    
    // Target = desired state (e.g., development)
    TargetConnectionString = "Server=127.0.0.1;Port=3306;Database=dev_aznv;Uid=root;Pwd=password;",
    
    OutputDirectory = ".",
    AllowDestructive = true,
    GenerateRollback = true
};

// =====================================================
// DEPENDENCY INJECTION
// =====================================================

var services = new ServiceCollection();

services.AddSingleton<SourceDataSourceProvider>(_ =>
    new SourceDataSourceProvider(new MySQLSource(config.SourceConnectionString)));

services.AddSingleton<TargetDataSourceProvider>(_ =>
    new TargetDataSourceProvider(new MySQLSource(config.TargetConnectionString)));

services.AddSingleton<SourceSchemaReader>(sp =>
    new SourceSchemaReader(new MySqlSchemaReader(sp.GetRequiredService<SourceDataSourceProvider>().Inner)));

services.AddSingleton<TargetSchemaReader>(sp =>
    new TargetSchemaReader(new MySqlSchemaReader(sp.GetRequiredService<TargetDataSourceProvider>().Inner)));

services.AddSingleton<SchemaDiffer>();
services.AddSingleton<ISqlEmitter, MySqlSqlEmitter>();
services.AddSingleton<SqlMigrationBuilder>();

var sp = services.BuildServiceProvider();

// =====================================================
// EXECUTE MIGRATION
// =====================================================

Console.WriteLine("Schema Migration Tool");
Console.WriteLine("====================");
Console.WriteLine();

try
{
    // 1. Read schemas
    Console.WriteLine("Reading source schema...");
    var source = await sp.GetRequiredService<SourceSchemaReader>().Reader.ReadAsync();
    Console.WriteLine($"  Found {source.Tables.Count} tables, {source.TotalColumns} columns");

    Console.WriteLine("Reading target schema...");
    var target = await sp.GetRequiredService<TargetSchemaReader>().Reader.ReadAsync();
    Console.WriteLine($"  Found {target.Tables.Count} tables, {target.TotalColumns} columns");
    Console.WriteLine();

    // 2. Diff schemas (SOURCE -> TARGET)
    Console.WriteLine("Computing differences...");
    var diffOptions = new DiffOptions
    {
        AllowDestructive = config.AllowDestructive,
        ExcludeTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "__schema_migrations",
            "__efmigrationshistory"
        }
    };

    var differ = sp.GetRequiredService<SchemaDiffer>();
    var plan = differ.Diff(source, target, diffOptions);

    // 3. Display summary
    var summary = plan.Summary;
    Console.WriteLine($"  Total operations: {summary.TotalOperations}");
    Console.WriteLine($"    Safe: {summary.SafeOperations}");
    Console.WriteLine($"    Risky: {summary.RiskyOperations}");
    Console.WriteLine($"    Destructive: {summary.DestructiveOperations}");
    Console.WriteLine($"  Warnings: {summary.Warnings}");
    Console.WriteLine();

    if (plan.IsEmpty)
    {
        Console.WriteLine("✅ Schemas are in sync. No migration needed.");
        return 0;
    }

    // 4. Display warnings
    if (plan.Warnings.Any())
    {
        Console.WriteLine("⚠️  WARNINGS:");
        foreach (var warning in plan.Warnings)
        {
            Console.WriteLine($"  {warning}");
        }
        Console.WriteLine();
    }

    // 5. Build SQL
    Console.WriteLine("Generating SQL...");
    var emitOptions = new EmitOptions
    {
        AllowDestructive = config.AllowDestructive,
        GenerateRollback = config.GenerateRollback,
        UseTransaction = true,
        IncludeComments = true,
        IncludeTimestamp = true,
        DisableForeignKeyChecks = plan.GetOperations(DiffOperationType.DropForeignKey).Any() ||
                                  plan.GetOperations(DiffOperationType.AddForeignKey).Any()
    };

    var builder = sp.GetRequiredService<SqlMigrationBuilder>();
    var output = builder.Build(plan, source, target, emitOptions);

    // 6. Write output files
    output.WriteToFiles(config.OutputDirectory);

    Console.WriteLine($"  ✅ migration.sql written");
    if (!string.IsNullOrEmpty(output.PrecheckSql))
        Console.WriteLine($"  ✅ prechecks.sql written");
    if (!string.IsNullOrEmpty(output.RollbackSql))
        Console.WriteLine($"  ✅ rollback.sql written");

    Console.WriteLine();
    Console.WriteLine("Done! Review the generated files before executing.");

    // Return non-zero if destructive operations detected
    return plan.HasDestructiveOperations ? 2 : 0;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"❌ Error: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"❌ Unexpected error: {ex}");
    return 1;
}

// =====================================================
// CONFIGURATION & DI TYPES
// =====================================================

internal sealed class MigrationConfig
{
    public required string SourceConnectionString { get; init; }
    public required string TargetConnectionString { get; init; }
    public string OutputDirectory { get; init; } = ".";
    public bool AllowDestructive { get; init; }
    public bool GenerateRollback { get; init; }
}

internal sealed record SourceDataSourceProvider(IDataSource Inner);
internal sealed record TargetDataSourceProvider(IDataSource Inner);

internal sealed record SourceSchemaReader(MySqlSchemaReader Reader);
internal sealed record TargetSchemaReader(MySqlSchemaReader Reader);

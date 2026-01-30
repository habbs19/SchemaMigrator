using DataAccessProvider.Core.Interfaces;
using DataAccessProvider.MySql;
using Microsoft.Extensions.DependencyInjection;
using SchemaMigrator.Core.Diff;
using SchemaMigrator.Core.Options;
using SchemaMigrator.Core.Sql;
using SchemaMigrator.MySql;

var services = new ServiceCollection();

// Create distinct concrete provider types so DI can differentiate them.
services.AddSingleton<ProdDataSourceProvider>(_ =>
{
    var cs = "Server=127.0.0.1;Port=3306;Database=dev_aznv;Uid=root;Pwd=password;";
    return new ProdDataSourceProvider(new MySQLSource(cs));
});

services.AddSingleton<DevDataSourceProvider>(_ =>
{
    var cs = "Server=127.0.0.1;Port=3306;Database=aznv;Uid=root;Pwd=password;";
    return new DevDataSourceProvider(new MySQLSource(cs));
});

services.AddSingleton<ProdSchemaReader>(sp =>
    new ProdSchemaReader(new MySqlSchemaReader(sp.GetRequiredService<ProdDataSourceProvider>().Inner)));

services.AddSingleton<DevSchemaReader>(sp =>
    new DevSchemaReader(new MySqlSchemaReader(sp.GetRequiredService<DevDataSourceProvider>().Inner)));

var sp = services.BuildServiceProvider();

var prod = await sp.GetRequiredService<ProdSchemaReader>().Reader.ReadAsync();
var dev = await sp.GetRequiredService<DevSchemaReader>().Reader.ReadAsync();


var diffOptions = new DiffOptions
{
    
};
var plan = new SchemaDiffer().Diff(dev, prod, diffOptions);
var emitter = new MySqlSqlEmitter();
var builder = new SqlMigrationBuilder(emitter);
var sql = builder.Build(plan, dev,prod, new EmitOptions
{
    AllowDestructive = true
});

File.WriteAllText("migration.sql", sql);

internal sealed record ProdDataSourceProvider(IDataSource Inner);
internal sealed record DevDataSourceProvider(IDataSource Inner);

internal sealed record ProdSchemaReader(MySqlSchemaReader Reader);
internal sealed record DevSchemaReader(MySqlSchemaReader Reader);

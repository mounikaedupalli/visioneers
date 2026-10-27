var builder = DistributedApplication.CreateBuilder(args);

// Local dev: points at SQL Server LocalDB via ConnectionStrings:phishguard in appsettings.json (no Docker needed).
// With Docker:  builder.AddSqlServer("sql").AddDatabase("phishguard")   (package Aspire.Hosting.SqlServer)
// On Azure:     builder.AddAzureSqlServer("sql").AddDatabase("phishguard") (package Aspire.Hosting.Azure.Sql)
var db = builder.AddConnectionString("phishguard");

var api = builder.AddProject<Projects.PhishGuard_Api>("api")
    .WithReference(db);

builder.AddProject<Projects.PhishGuard_Scoring>("scoring")
    .WithReference(db);

builder.AddProject<Projects.PhishGuard_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();

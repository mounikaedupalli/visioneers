using PhishGuard.Data;
using PhishGuard.Scoring;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<PhishGuardDbContext>("phishguard");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

using PhishGuard.Agents;
using PhishGuard.Agents.Investigation;
using PhishGuard.Data;
using PhishGuard.Scoring;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<PhishGuardDbContext>("phishguard");
builder.Services.AddHostedService<Worker>();
builder.Services.AddInvestigation(builder.Configuration.GetSection("Investigation").Get<InvestigationOptions>());
builder.Services.AddHostedService<InvestigationWorker>();

var host = builder.Build();
host.Run();

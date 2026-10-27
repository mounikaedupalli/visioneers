using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using PhishGuard.Agents;
using PhishGuard.Agents.Investigation;
using PhishGuard.Api;
using PhishGuard.Core.Domain;
using PhishGuard.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<PhishGuardDbContext>("phishguard");
builder.Services.AddOpenApi();
builder.Services.AddInvestigation(builder.Configuration.GetSection("Investigation").Get<InvestigationOptions>());
// Enums as strings in requests and responses ("Phishing", not 1).
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapDefaultEndpoints();

var api = app.MapGroup("/api");

// Dataset overview: counts per category, used by the dashboard funnel.
api.MapGet("/stats", async (PhishGuardDbContext db) =>
{
    var byCategory = await db.Urls
        .GroupBy(u => new { u.Category, u.IsPhishing })
        .Select(g => new CategoryCount(g.Key.Category.ToString(), g.Key.IsPhishing, g.Count()))
        .ToListAsync();
    return new DatasetStats(byCategory.Sum(c => c.Count), byCategory.Where(c => c.IsPhishing).Sum(c => c.Count),
        await db.DomainEvidence.CountAsync(), byCategory.OrderByDescending(c => c.Count).ToList());
});

api.MapGet("/brands", (PhishGuardDbContext db) => db.Brands.AsNoTracking().OrderBy(b => b.Name).ToListAsync());

api.MapGet("/urls", async (PhishGuardDbContext db, UrlCategory? category, int page = 1, int pageSize = 50) =>
{
    pageSize = Math.Clamp(pageSize, 1, 500);
    var q = db.Urls.AsNoTracking();
    if (category is not null) q = q.Where(u => u.Category == category);
    return await q.OrderBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize)
        .Select(u => new { u.Id, u.Url, u.RegisteredDomain, Category = u.Category.ToString(), u.IsPhishing, u.TargetBrandId, u.IngestedAtUtc })
        .ToListAsync();
});

api.MapGet("/evidence/{domain}", async (PhishGuardDbContext db, string domain) =>
    await db.DomainEvidence.FindAsync(domain) is { } e ? Results.Ok(e) : Results.NotFound());

api.MapResultsEndpoints();  // /scoring/summary, /phishing, /campaigns, /campaigns/{id}
api.MapAnalystEndpoints();  // /dashboard, /cases, /cases/{id}, /cases/{id}/investigate, /cases/{id}/decision, /notices, /indicators


app.Run();

public record CategoryCount(string Category, bool IsPhishing, int Count);
public record DatasetStats(int TotalUrls, int PhishingUrls, int DomainsWithEvidence, List<CategoryCount> ByCategory);

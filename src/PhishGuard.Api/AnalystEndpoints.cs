using System.Text;
using Microsoft.EntityFrameworkCore;
using PhishGuard.Agents.Investigation;
using PhishGuard.Core.Domain;
using PhishGuard.Data;

namespace PhishGuard.Api;

/// <summary>Endpoints behind the analyst screens: dashboard, case queue, case investigation, enforcement, block-list.</summary>
public static class AnalystEndpoints
{
    public record DecisionRequest(Verdict Decision, string Analyst, string? Notes);
    public record NoticeActionRequest(string Analyst);

    public static RouteGroupBuilder MapAnalystEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", async (PhishGuardDbContext db) =>
        {
            var version = await db.Scores.OrderByDescending(s => s.Id).Select(s => s.ModelVersion).FirstOrDefaultAsync();
            var bands = version is null ? [] : await db.Scores.Where(s => s.ModelVersion == version)
                .GroupBy(s => s.Band).Select(g => new { Band = g.Key, Count = g.Count() }).ToListAsync();
            var cases = await db.Cases.GroupBy(c => c.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
            var verdicts = await db.Cases.GroupBy(c => c.Verdict).Select(g => new { Verdict = g.Key, Count = g.Count() }).ToListAsync();
            var notices = await db.EnforcementNotices.GroupBy(n => n.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
            var investigated = await db.Cases.CountAsync(c => c.AgentRuns.Any());
            var decided = await db.AnalystDecisions.Select(d => d.CaseId).Distinct().CountAsync();
            var topBrands = await db.Cases.Where(c => c.Status == CaseStatus.AnalystReview || c.Status == CaseStatus.Confirmed)
                .GroupBy(c => c.Campaign!.Brand!.Name).Select(g => new { Brand = g.Key ?? "Unknown", Cases = g.Count() })
                .OrderByDescending(g => g.Cases).Take(8).ToListAsync();
            return new
            {
                modelVersion = version,
                totalScored = bands.Sum(b => b.Count),
                bands = Enum.GetValues<ScoreBand>().ToDictionary(b => b.ToString(), b => bands.Where(x => x.Band == b).Sum(x => x.Count)),
                casesByStatus = Enum.GetValues<CaseStatus>().ToDictionary(s => s.ToString(), s => cases.Where(x => x.Status == s).Sum(x => x.Count)),
                casesByVerdict = Enum.GetValues<Verdict>().ToDictionary(v => v.ToString(), v => verdicts.Where(x => x.Verdict == v).Sum(x => x.Count)),
                noticesByStatus = Enum.GetValues<NoticeStatus>().ToDictionary(s => s.ToString(), s => notices.Where(x => x.Status == s).Sum(x => x.Count)),
                investigated,
                analystDecided = decided,
                indicators = await db.Indicators.CountAsync(),
                topBrands
            };
        });

        api.MapGet("/cases", async (PhishGuardDbContext db, CaseStatus? status, Verdict? verdict, int? brandId, string? search, int page = 1, int pageSize = 50) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            var q = db.Cases.AsNoTracking();
            if (status is not null) q = q.Where(c => c.Status == status);
            if (verdict is not null) q = q.Where(c => c.Verdict == verdict);
            if (brandId is not null) q = q.Where(c => c.Campaign!.BrandId == brandId);
            if (!string.IsNullOrWhiteSpace(search)) q = q.Where(c => c.Campaign!.RegisteredDomain.Contains(search));
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(c => c.Campaign!.UrlCount).ThenByDescending(c => c.Campaign!.MaxProbability).ThenBy(c => c.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    status = c.Status.ToString(),
                    verdict = c.Verdict.ToString(),
                    c.Confidence,
                    c.Campaign!.RegisteredDomain,
                    c.Campaign.PathPattern,
                    brand = c.Campaign.Brand!.Name,
                    c.Campaign.UrlCount,
                    modelProbability = c.Campaign.MaxProbability,
                    investigated = c.AgentRuns.Any(),
                    c.UpdatedAtUtc
                }).ToListAsync();
            return new { total, page, pageSize, items };
        });

        api.MapGet("/cases/{id:long}", async (PhishGuardDbContext db, long id) =>
        {
            var c = await db.Cases.AsNoTracking().Include(x => x.Campaign!).ThenInclude(m => m.Brand).FirstOrDefaultAsync(x => x.Id == id);
            if (c?.Campaign is null) return Results.NotFound();
            var urls = await db.Scores.AsNoTracking().Where(s => s.CampaignId == c.CampaignId).OrderByDescending(s => s.Probability).Take(100)
                .Select(s => new { s.UrlId, s.Url!.Url, s.Probability, band = s.Band.ToString(), s.Url.IngestedAtUtc }).ToListAsync();
            var shortUrls = urls.Select(u => u.Url).Where(u => u.Length <= 400).ToList();
            var expansions = await db.ShortLinks.AsNoTracking().Where(s => shortUrls.Contains(s.ShortUrl)).ToDictionaryAsync(s => s.ShortUrl, s => s.ExpandedUrl);
            var findings = await CaseInvestigator.LatestFindingsAsync(db, id);
            return Results.Ok(new
            {
                c.Id,
                status = c.Status.ToString(),
                verdict = c.Verdict.ToString(),
                c.Confidence,
                c.Rationale,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                campaign = new { c.Campaign.Id, c.Campaign.RegisteredDomain, c.Campaign.PathPattern, brand = c.Campaign.Brand?.Name, c.Campaign.UrlCount, c.Campaign.MaxProbability },
                evidence = await db.DomainEvidence.AsNoTracking().FirstOrDefaultAsync(e => e.RegisteredDomain == c.Campaign.RegisteredDomain),
                steps = findings.Count > 0 ? findings : PendingSteps(),
                urls = urls.Select(u => new { u.UrlId, u.Url, expandedUrl = expansions.GetValueOrDefault(u.Url), u.Probability, u.band, u.IngestedAtUtc }),
                notices = await db.EnforcementNotices.AsNoTracking().Where(n => n.CaseId == id).OrderBy(n => n.Id)
                    .Select(n => new { n.Id, route = n.Route.ToString(), n.Recipient, n.Subject, n.Body, status = n.Status.ToString(), n.CreatedAtUtc, n.FiledAtUtc }).ToListAsync(),
                decisions = await db.AnalystDecisions.AsNoTracking().Where(d => d.CaseId == id).OrderByDescending(d => d.Id)
                    .Select(d => new { d.Analyst, decision = d.Decision.ToString(), d.Notes, d.DecidedAtUtc }).ToListAsync(),
                indicators = await db.Indicators.AsNoTracking().CountAsync(i => i.CaseId == id)
            });
        });

        api.MapPost("/cases/{id:long}/investigate", async (CaseInvestigator investigator, long id, CancellationToken ct) =>
            await investigator.InvestigateAsync(id, ct) is { } r ? Results.Ok(r) : Results.NotFound());

        api.MapPost("/cases/{id:long}/decision", async (PhishGuardDbContext db, CaseInvestigator investigator, long id, DecisionRequest req, CancellationToken ct) =>
        {
            if (req.Decision is not (Verdict.Phishing or Verdict.Benign)) return Results.BadRequest("Decision must be Phishing or Benign.");
            if (string.IsNullOrWhiteSpace(req.Analyst)) return Results.BadRequest("Analyst name is required.");
            if (!await db.Cases.AnyAsync(c => c.Id == id, ct)) return Results.NotFound();
            db.AnalystDecisions.Add(new AnalystDecision { CaseId = id, Analyst = req.Analyst, Decision = req.Decision, Notes = req.Notes, DecidedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            return Results.Ok(await investigator.InvestigateAsync(id, ct, req.Decision, req.Analyst));
        });

        api.MapGet("/notices", async (PhishGuardDbContext db, NoticeStatus? status, int page = 1, int pageSize = 50) =>
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            var q = db.EnforcementNotices.AsNoTracking();
            if (status is not null) q = q.Where(n => n.Status == status);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(n => n.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Join(db.Cases, n => n.CaseId, c => c.Id, (n, c) => new
                {
                    n.Id, n.CaseId, route = n.Route.ToString(), n.Recipient, n.Subject, n.Body, status = n.Status.ToString(),
                    n.CreatedAtUtc, n.FiledAtUtc, domain = c.Campaign!.RegisteredDomain, brand = c.Campaign.Brand!.Name
                }).ToListAsync();
            return new { total, page, pageSize, items };
        });

        // Filing is simulated: the notice is marked as filed and the case moves to TakedownFiled. Nothing is sent.
        api.MapPost("/notices/{id:long}/approve", async (PhishGuardDbContext db, long id, NoticeActionRequest req) =>
        {
            var n = await db.EnforcementNotices.FindAsync(id);
            if (n is null) return Results.NotFound();
            if (n.Status != NoticeStatus.Draft) return Results.BadRequest($"Notice is already {n.Status}.");
            n.Status = NoticeStatus.Filed;
            n.FiledAtUtc = DateTime.UtcNow;
            var c = await db.Cases.FindAsync(n.CaseId);
            if (c is not null && c.Status == CaseStatus.Confirmed) { c.Status = CaseStatus.TakedownFiled; c.UpdatedAtUtc = DateTime.UtcNow; }
            db.AgentRuns.Add(new AgentRun { CaseId = n.CaseId, AgentName = "Analyst", Input = $"approve notice {id}", Output = $"Filed by {req.Analyst} (simulated)", StartedAtUtc = DateTime.UtcNow, CompletedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return Results.Ok(new { n.Id, status = n.Status.ToString() });
        });

        api.MapPost("/notices/{id:long}/reject", async (PhishGuardDbContext db, long id, NoticeActionRequest req) =>
        {
            var n = await db.EnforcementNotices.FindAsync(id);
            if (n is null) return Results.NotFound();
            if (n.Status != NoticeStatus.Draft) return Results.BadRequest($"Notice is already {n.Status}.");
            n.Status = NoticeStatus.Rejected;
            db.AgentRuns.Add(new AgentRun { CaseId = n.CaseId, AgentName = "Analyst", Input = $"reject notice {id}", Output = $"Rejected by {req.Analyst}", StartedAtUtc = DateTime.UtcNow, CompletedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return Results.Ok(new { n.Id, status = n.Status.ToString() });
        });

        api.MapGet("/indicators", async (PhishGuardDbContext db, IndicatorType? type, string format = "json", int page = 1, int pageSize = 100) =>
        {
            var q = db.Indicators.AsNoTracking();
            if (type is not null) q = q.Where(i => i.Type == type);
            var csv = format.Equals("csv", StringComparison.OrdinalIgnoreCase);
            pageSize = csv ? 1_000_000 : Math.Clamp(pageSize, 1, 500);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(i => i.Id).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(i => new { i.Id, type = i.Type.ToString(), i.Value, i.CaseId, i.Source, i.AddedAtUtc }).ToListAsync();
            if (!csv) return Results.Ok(new { total, page, pageSize, items });
            var sb = new StringBuilder("type,value,case_id,added_at_utc\n");
            foreach (var i in items) sb.Append(i.type).Append(",\"").Append(i.Value.Replace("\"", "\"\"")).Append("\",").Append(i.CaseId).Append(',').Append(i.AddedAtUtc.ToString("O")).Append('\n');
            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "phishguard-blocklist.csv");
        });

        return api;
    }

    private static IReadOnlyList<StepFinding> PendingSteps() =>
        CaseInvestigator.EvidenceSteps.Select(s => new StepFinding(s.Number, s.Name, FindingLevel.Unknown, 0, "Not investigated yet", []))
            .Append(new StepFinding(6, CaseInvestigator.VerdictStepName, FindingLevel.Unknown, 0, "Not investigated yet", []))
            .Append(new StepFinding(7, CaseInvestigator.EnforcementStepName, FindingLevel.Unknown, 0, "Not investigated yet", []))
            .ToList();
}

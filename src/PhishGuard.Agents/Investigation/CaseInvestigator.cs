using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhishGuard.Core.Domain;
using PhishGuard.Core.Urls;
using PhishGuard.Data;

namespace PhishGuard.Agents.Investigation;

/// <summary>
/// Runs the analysts' standard operating procedure on a case the model has flagged (Gray or Confirmed band):
///   1–5  evidence steps: pluggable <see cref="IInvestigationStep"/> implementations, run in order of Number
///   6    verdict: model score + step points → confidence → Phishing / Benign / NeedsHuman
///   7    enforcement: draft takedown notices and record block-list indicators (never sent; an analyst approves)
/// Each step's finding is stored as an AgentRun so the case screen can show the full checklist.
/// </summary>
public sealed class CaseInvestigator(PhishGuardDbContext db, IEnumerable<IInvestigationStep> steps, InvestigationOptions options)
{
    /// <summary>The SOP steps 1–5. Any without an implementation are shown as "not implemented" and score 0.</summary>
    public static readonly (int Number, string Name)[] EvidenceSteps =
    [
        (1, "Syntax & domain inspection"),
        (2, "Infrastructure & hosting"),
        (3, "Content & layout evaluation"),
        (4, "Behavioral test (sandbox)"),
        (5, "SSL/TLS certificate audit")
    ];

    public const string VerdictStepName = "Confirmation & triage verdict";

    /// <summary>Summary of a step that has neither an automated implementation nor an analyst's assessment yet.</summary>
    public const string AwaitingAssessment = "Waiting for analyst assessment";
    public const string EnforcementStepName = "Mitigation & enforcement";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private static readonly CaseStatus[] AnalystOwned = [CaseStatus.Dismissed, CaseStatus.TakedownFiled, CaseStatus.Closed];

    private readonly IReadOnlyList<IInvestigationStep> _steps = steps.OrderBy(s => s.Number).ToList();

    /// <param name="analystVerdict">An analyst's decision (Phishing or Benign): replaces the automatic verdict and status.</param>
    public async Task<InvestigationResult?> InvestigateAsync(long caseId, CancellationToken ct = default, Verdict? analystVerdict = null, string? analyst = null)
    {
        var context = await LoadContextAsync(caseId, ct);
        if (context is null) return null;

        var started = DateTime.UtcNow;
        // An analyst's own assessment of a step wins over the automated step (or the "not implemented" placeholder).
        var byAnalyst = (await LatestFindingsAsync(db, caseId, ct)).Where(f => f.RecordedBy is not null).ToDictionary(f => f.Step);
        var findings = new List<StepFinding>();
        foreach (var (number, name) in EvidenceSteps)
        {
            var step = _steps.FirstOrDefault(s => s.Number == number);
            findings.Add(byAnalyst.TryGetValue(number, out var manual) ? manual
                : step is null
                    ? new StepFinding(number, name, FindingLevel.Unknown, 0, AwaitingAssessment, [])
                    : step.Run(context));
        }

        var (verdict, confidence, rationale) = DecideVerdict(context, findings);
        var status = NextStatus(context, verdict, findings);
        if (analystVerdict is Verdict.Phishing or Verdict.Benign)
        {
            (verdict, confidence) = (analystVerdict.Value, 1.0);
            status = verdict == Verdict.Phishing ? CaseStatus.Confirmed : CaseStatus.Dismissed;
            rationale = $"Decided by analyst {analyst ?? "unknown"}. Automatic assessment: {rationale}";
        }
        findings.Add(new StepFinding(6, VerdictStepName, verdict switch
            {
                Verdict.Phishing => FindingLevel.Malicious,
                Verdict.Benign => FindingLevel.Benign,
                _ => FindingLevel.Suspicious
            }, 0, $"{verdict} ({confidence:P0} confidence)", [rationale]));

        var (notices, indicators) = status == CaseStatus.Confirmed ? DraftEnforcement(context) : ([], []);
        findings.Add(new StepFinding(7, EnforcementStepName,
            notices.Count > 0 ? FindingLevel.Malicious : FindingLevel.Unknown, 0,
            notices.Count > 0 ? $"{notices.Count} notice(s) drafted for approval; indicators added to the block-list" : "No enforcement (not confirmed)",
            notices.Select(n => $"{n.Route}: {n.Recipient}").ToList()));

        await ApplyAsync(context, findings, verdict, confidence, rationale, status, notices, indicators, started, ct);
        return new InvestigationResult(caseId, findings, verdict, confidence, rationale, status, notices, indicators);
    }

    private async Task<InvestigationContext?> LoadContextAsync(long caseId, CancellationToken ct)
    {
        var @case = await db.Cases.Include(c => c.Campaign!).ThenInclude(m => m.Brand).FirstOrDefaultAsync(c => c.Id == caseId, ct);
        if (@case?.Campaign is null) return null;
        var campaign = @case.Campaign;

        var urls = await db.Scores.AsNoTracking().Where(s => s.CampaignId == campaign.Id)
            .OrderByDescending(s => s.Probability).Take(options.MaxUrls)
            .Select(s => new { s.Url!.Url, s.Probability }).ToListAsync(ct);
        var shortUrls = urls.Select(u => u.Url).Where(u => u.Length <= 400).ToList();
        var expansions = await db.ShortLinks.AsNoTracking().Where(s => shortUrls.Contains(s.ShortUrl))
            .ToDictionaryAsync(s => s.ShortUrl, s => s.ExpandedUrl, ct);
        var brands = await db.Brands.AsNoTracking().ToListAsync(ct);
        var band = campaign.GroupingKey.Split('|') is { Length: > 1 } parts && Enum.TryParse<ScoreBand>(parts[1], out var b) ? b : ScoreBand.Gray;

        return new InvestigationContext
        {
            Case = @case,
            Campaign = campaign,
            Brand = campaign.Brand,
            Brands = brands,
            Extractor = new UrlFeatureExtractor(brands),
            Urls = urls.Select(u => (u.Url, expansions.GetValueOrDefault(u.Url, u.Url), u.Probability)).ToList(),
            Evidence = await db.DomainEvidence.AsNoTracking().FirstOrDefaultAsync(e => e.RegisteredDomain == campaign.RegisteredDomain, ct),
            ModelProbability = campaign.MaxProbability,
            ModelBand = band
        };
    }

    /// <summary>Step 6: model score and step points add up to a confidence; thresholds decide what needs a human.</summary>
    private (Verdict, double, string) DecideVerdict(InvestigationContext c, IReadOnlyList<StepFinding> findings)
    {
        var modelPoints = options.ModelWeight * (2 * c.ModelProbability - 1);
        var total = modelPoints + findings.Sum(f => f.Points);
        var confidence = 1 / (1 + Math.Exp(-total / 2));

        var verdict = confidence >= options.PhishingThreshold ? Verdict.Phishing
            : confidence <= options.BenignThreshold ? Verdict.Benign
            : Verdict.NeedsHuman;

        var reasons = findings.Where(f => f.Points != 0).OrderByDescending(f => Math.Abs(f.Points))
            .Select(f => $"step {f.Step} {f.Summary.ToLowerInvariant()} ({f.Points:+0.0;-0.0})").Take(3).ToList();
        var manual = findings.Count(f => f.RecordedBy is not null);
        var automated = findings.Count(f => f.RecordedBy is null && f.Summary != AwaitingAssessment && f.Summary != "Not implemented yet");
        var rationale = $"Model score {c.ModelProbability:P0} ({c.ModelBand} band, {modelPoints:+0.0;-0.0} points)"
                        + (reasons.Count > 0 ? "; " + string.Join("; ", reasons) : "")
                        + $". Evidence steps: {manual} assessed by an analyst, {automated} automated, {EvidenceSteps.Length - manual - automated} not done.";
        return (verdict, confidence, rationale);
    }

    /// <summary>
    /// Clear-cut verdicts close the case automatically; everything else goes to an analyst. A campaign the model already
    /// auto-confirmed stays confirmed unless the evidence steps point to benign. Analyst decisions are never overwritten.
    /// </summary>
    private static CaseStatus NextStatus(InvestigationContext c, Verdict verdict, IReadOnlyList<StepFinding> findings)
    {
        if (AnalystOwned.Contains(c.Case.Status)) return c.Case.Status;
        var benignEvidence = findings.Where(f => f.Step <= 5).Sum(f => f.Points) < 0;
        return verdict switch
        {
            Verdict.Phishing => CaseStatus.Confirmed,
            Verdict.Benign => CaseStatus.Dismissed,
            _ when c.ModelBand == ScoreBand.Confirmed && !benignEvidence => CaseStatus.Confirmed,
            _ => CaseStatus.AnalystReview
        };
    }

    /// <summary>Step 7: drafts only. Recipients come from the domain evidence; nothing is sent.</summary>
    private static (List<EnforcementNotice>, List<Indicator>) DraftEnforcement(InvestigationContext c)
    {
        var now = DateTime.UtcNow;
        var e = c.Evidence;
        var brand = c.Brand?.Name ?? "our client";
        var sample = string.Join("\n", c.Urls.Take(10).Select(u => "  " + Defang(u.Effective)));
        var notices = new List<EnforcementNotice>();

        void Draft(EnforcementRoute route, string? recipient, string subject)
        {
            if (string.IsNullOrWhiteSpace(recipient)) return;
            notices.Add(new EnforcementNotice
            {
                CaseId = c.Case.Id,
                Route = route,
                Recipient = recipient,
                Subject = subject,
                Body = $"""
                    Hello,

                    We are reporting a phishing campaign impersonating {brand} on {c.Campaign.RegisteredDomain}.
                    {c.Campaign.UrlCount} URL(s) observed; examples (defanged):
                    {sample}

                    Evidence summary: registered {e?.CreatedOnUtc:yyyy-MM-dd} via {e?.Registrar ?? "unknown registrar"}; hosting {e?.HostingProvider ?? "unknown"}; certificate issued by {e?.CertIssuer ?? "unknown"}.
                    Please suspend the domain / content and preserve related logs.

                    Case reference: PG-{c.Case.Id}
                    """,
                Status = NoticeStatus.Draft,
                CreatedAtUtc = now
            });
        }

        Draft(EnforcementRoute.Registrar, e?.RegistrarAbuseEmail, $"Phishing abuse report: {c.Campaign.RegisteredDomain} (PG-{c.Case.Id})");
        Draft(EnforcementRoute.HostingProvider, e?.HostingAbuseEmail, $"Phishing content hosted on your network: {c.Campaign.RegisteredDomain} (PG-{c.Case.Id})");
        if (e?.CertIssuer is { } issuer)
            Draft(EnforcementRoute.CertificateAuthority, $"Certificate problem report — {issuer}", $"Certificate used for phishing: {c.Campaign.RegisteredDomain} (PG-{c.Case.Id})");

        var indicators = new List<Indicator>();
        // Never block a whole domain that is the brand's own or a legitimate site abused as an open redirect.
        var openRedirect = c.Urls.Any(u => UrlParser.TryParse(u.Effective, out var p) && p.Query.Contains("http", StringComparison.OrdinalIgnoreCase));
        if (!c.IsBrandOwned && !openRedirect)
            indicators.Add(new Indicator { Type = IndicatorType.Domain, Value = c.Campaign.RegisteredDomain, CaseId = c.Case.Id, Source = "investigation", AddedAtUtc = now });
        foreach (var u in c.Urls.Select(u => u.Url).Distinct().Take(50))
            indicators.Add(new Indicator { Type = IndicatorType.Url, Value = u, CaseId = c.Case.Id, Source = "investigation", AddedAtUtc = now });
        return (notices, indicators);
    }

    private async Task ApplyAsync(InvestigationContext c, IReadOnlyList<StepFinding> findings, Verdict verdict, double confidence,
        string rationale, CaseStatus status, List<EnforcementNotice> notices, List<Indicator> indicators, DateTime started, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        db.AgentRuns.AddRange(findings.Select(f => new AgentRun
        {
            CaseId = c.Case.Id,
            AgentName = $"Step{f.Step}",
            Input = c.RepresentativeUrl,
            Output = JsonSerializer.Serialize(f, Json),
            StartedAtUtc = started,
            CompletedAtUtc = now
        }));

        // Re-running replaces earlier drafts that nobody has acted on yet.
        await db.EnforcementNotices.Where(n => n.CaseId == c.Case.Id && n.Status == NoticeStatus.Draft).ExecuteDeleteAsync(ct);
        db.EnforcementNotices.AddRange(notices);

        var values = indicators.Select(i => i.Value).ToList();
        var known = await db.Indicators.Where(i => values.Contains(i.Value)).Select(i => new { i.Type, i.Value }).ToListAsync(ct);
        db.Indicators.AddRange(indicators.Where(i => !known.Any(k => k.Type == i.Type && k.Value == i.Value)));
        if (status == CaseStatus.Dismissed) // a dismissed case must not keep anything on the block-list
            await db.Indicators.Where(i => i.CaseId == c.Case.Id).ExecuteDeleteAsync(ct);

        c.Case.Verdict = verdict;
        c.Case.Confidence = (float)confidence;
        c.Case.Rationale = rationale;
        c.Case.Status = status;
        c.Case.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Saves an analyst's own assessment of one evidence step (1–5). It is kept in the case's audit trail (AgentRuns)
    /// and used by every later investigation of the case. Does not change the case status by itself.
    /// </summary>
    public async Task<StepFinding?> RecordAssessmentAsync(long caseId, int step, FindingLevel level, string analyst, string? notes, CancellationToken ct = default)
    {
        var name = EvidenceSteps.FirstOrDefault(s => s.Number == step).Name;
        if (name is null || !await db.Cases.AnyAsync(c => c.Id == caseId, ct)) return null;

        var now = DateTime.UtcNow;
        var details = (notes ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var finding = new StepFinding(step, name, level, StepFinding.PointsFor(level),
            level == FindingLevel.Unknown ? "Analyst: not sure" : $"Analyst assessed as {level.ToString().ToLowerInvariant()}",
            details, analyst, now);
        db.AgentRuns.Add(new AgentRun
        {
            CaseId = caseId,
            AgentName = $"Step{step}",
            Input = $"analyst:{analyst}",
            Output = JsonSerializer.Serialize(finding, Json),
            StartedAtUtc = now,
            CompletedAtUtc = now
        });
        await db.SaveChangesAsync(ct);
        return finding;
    }

    /// <summary>hxxps://example[.]com — safe to paste into notices and chat.</summary>
    public static string Defang(string url) => url.Replace("http", "hxxp", StringComparison.OrdinalIgnoreCase).Replace(".", "[.]");

    /// <summary>Latest finding per step for a case, for the case screen.</summary>
    public static async Task<IReadOnlyList<StepFinding>> LatestFindingsAsync(PhishGuardDbContext db, long caseId, CancellationToken ct = default)
    {
        var runs = await db.AgentRuns.AsNoTracking().Where(r => r.CaseId == caseId && r.AgentName.StartsWith("Step"))
            .OrderByDescending(r => r.Id).ToListAsync(ct);
        return runs.GroupBy(r => r.AgentName).Select(g => JsonSerializer.Deserialize<StepFinding>(g.First().Output ?? "null", Json))
            .OfType<StepFinding>().OrderBy(f => f.Step).ToList();
    }
}

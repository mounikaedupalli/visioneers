using Microsoft.EntityFrameworkCore;
using PhishGuard.Agents.Investigation;
using PhishGuard.Core.Domain;
using PhishGuard.Data;

namespace PhishGuard.Scoring;

/// <summary>
/// Runs the analysts' procedure automatically after the model has flagged URLs: every new case
/// (Gray → AgentReview, or auto-Confirmed with no investigation yet) is investigated once. Clear URLs never get here.
/// Turn off with Investigation:Enabled = false (e.g. when a teammate already runs it against the shared database).
/// </summary>
public class InvestigationWorker(ILogger<InvestigationWorker> logger, IServiceScopeFactory scopes, IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Investigation:Enabled", true))
        {
            logger.LogInformation("Investigation worker disabled (Investigation:Enabled = false)");
            return;
        }
        var batchSize = config.GetValue("Investigation:BatchSize", 200);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var done = await InvestigateBatchAsync(batchSize, stoppingToken);
                if (done > 0) continue; // more may be waiting
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Investigation run failed; retrying in 30 seconds");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task<int> InvestigateBatchAsync(int batchSize, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PhishGuardDbContext>();
        var pending = await db.Cases.AsNoTracking()
            .Where(c => (c.Status == CaseStatus.AgentReview || c.Status == CaseStatus.Confirmed) && !c.AgentRuns.Any())
            .OrderByDescending(c => c.Campaign!.UrlCount).Select(c => c.Id).Take(batchSize).ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var outcomes = new Dictionary<CaseStatus, int>();
        foreach (var id in pending)
        {
            using var caseScope = scopes.CreateScope(); // fresh DbContext per case keeps the change tracker small
            var investigator = caseScope.ServiceProvider.GetRequiredService<CaseInvestigator>();
            var result = await investigator.InvestigateAsync(id, ct);
            if (result is not null) outcomes[result.Status] = outcomes.GetValueOrDefault(result.Status) + 1;
        }
        logger.LogInformation("Investigated {Count} case(s): {Outcomes}", pending.Count,
            string.Join(", ", outcomes.Select(o => $"{o.Value} {o.Key}")));
        return pending.Count;
    }
}

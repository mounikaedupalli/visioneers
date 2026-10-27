using Microsoft.Extensions.DependencyInjection;
using PhishGuard.Agents.Investigation;

namespace PhishGuard.Agents;

/// <summary>
/// Registers the investigation pipeline. Evidence steps (1–5 of the analysts' procedure) are pluggable:
/// add a class implementing <see cref="IInvestigationStep"/> and register it here, e.g.
/// <c>services.AddScoped&lt;IInvestigationStep, MyInfrastructureStep&gt;();</c>.
/// Steps read SQL Server only (DomainEvidence, ShortLinks, Urls) — never the live site.
/// </summary>
public static class AgentCatalog
{
    public static IServiceCollection AddInvestigation(this IServiceCollection services, InvestigationOptions? options = null)
    {
        services.AddSingleton(options ?? new InvestigationOptions());
        services.AddScoped<CaseInvestigator>();
        return services;
    }
}

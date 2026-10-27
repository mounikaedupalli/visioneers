using PhishGuard.Web;
using PhishGuard.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// "https+http://api" is resolved by Aspire service discovery to the Api project.
builder.Services.AddHttpClient<PhishGuardApiClient>(c => c.BaseAddress = new Uri("https+http://api"));
// Who is recording decisions. Replace with Entra ID sign-in (Microsoft.Identity.Web) before real use.
builder.Services.AddSingleton(new AnalystIdentity(builder.Configuration["Analyst:Name"] ?? Environment.UserName));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

// The browser can't reach the API directly (service discovery), so the block-list download is proxied here.
app.MapGet("/download/blocklist.csv", async (PhishGuardApiClient api, CancellationToken ct) =>
    Results.File(await api.GetBlocklistCsvAsync(ct), "text/csv", "phishguard-blocklist.csv"));

app.Run();

public sealed record AnalystIdentity(string Name);


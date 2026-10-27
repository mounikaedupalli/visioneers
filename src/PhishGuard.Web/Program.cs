using PhishGuard.Web;
using PhishGuard.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// "https+http://api" is resolved by Aspire service discovery to the Api project.
builder.Services.AddHttpClient<PhishGuardApiClient>(c => c.BaseAddress = new Uri("https+http://api"));

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

app.Run();

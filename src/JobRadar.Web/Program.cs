using JobRadar.Web;
using JobRadar.Web.Components;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
// The worker's profiles, copied by the build: capturing a full ad rescores the listing here.
builder.Configuration.AddJsonFile("profiles.json", optional: true, reloadOnChange: false);
// Machine-specific settings (the Tailscale address to listen on) stay out of git.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
builder.Services.AddJobRadarWeb(builder.Configuration);
builder.Services.AddWindowsService(o => o.ServiceName = "JobRadarWeb");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
// For the watchdog (deploy/watchdog.ps1): 200 while the database answers and a pass ran within 26 h, 503 otherwise.
// No listing data, only the pass time.
app.MapGet("/health", async (JobRadar.Application.Operations.HealthService health, CancellationToken ct) =>
{
    var report = await health.CheckAsync(ct);
    return Results.Json(report, statusCode: report.Healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

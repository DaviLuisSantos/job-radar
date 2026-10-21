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
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

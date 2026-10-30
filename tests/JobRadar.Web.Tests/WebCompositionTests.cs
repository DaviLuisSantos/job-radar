using JobRadar.Application.Matching;
using JobRadar.Application.Tracking;
using JobRadar.Application.Triage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobRadar.Web.Tests;

public class WebCompositionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("http://localhost:4317")]
    public void The_site_container_builds_and_resolves_what_the_pages_use(string? otlpEndpoint)
    {
        // Same host as Program.cs: Blazor needs the web host services (environment, configuration).
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:JobRadar"] = "Host=localhost;Database=unused",
            ["Telemetry:OtlpEndpoint"] = otlpEndpoint,
        });
        builder.Host.UseDefaultServiceProvider(o => (o.ValidateOnBuild, o.ValidateScopes) = (true, true));
        builder.Services.AddJobRadarWeb(builder.Configuration);

        using var app = builder.Build();
        using var scope = app.Services.CreateScope();

        Assert.NotNull(app.Services.GetRequiredService<Backend>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TriageService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TrackingService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CalibrationService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<JobRadar.Application.Operations.HealthService>());
    }
}

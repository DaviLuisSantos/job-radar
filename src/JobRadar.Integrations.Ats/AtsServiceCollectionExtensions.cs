using JobRadar.Application.Abstractions;
using JobRadar.Integrations.Ats.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Ats;

public static class AtsServiceCollectionExtensions
{
    public static IServiceCollection AddAtsIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AtsOptions>()
            .Bind(configuration.GetSection(AtsOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AtsPacing>();
        services.AddTransient<AtsPacingHandler>();
        services.AddSingleton<AtsSnapshot>();

        var http = services.AddHttpClient<GreenhouseBoardClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<AtsOptions>>().Value;
            client.BaseAddress = o.BaseAddress;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        });
        // Retries 429 and 5xx with exponential backoff, honouring Retry-After. A board is a few megabytes at most.
        http.AddStandardResilienceHandler(r =>
        {
            r.Retry.MaxRetryAttempts = 3;
            r.Retry.Delay = TimeSpan.FromSeconds(5);
            r.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
            r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            r.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
        });
        // Inside the resilience handler: retries go through the budget too.
        http.AddHttpMessageHandler<AtsPacingHandler>();

        services.AddScoped<IBoardSweep, AtsBoardSweepService>();
        services.AddTransient<IJobSource, GreenhouseJobSource>();
        services.AddTransient<IDescriptionSource, GreenhouseDescriptionSource>();
        return services;
    }
}

using JobRadar.Application.Abstractions;
using JobRadar.Integrations.Gupy.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Gupy;

public static class GupyServiceCollectionExtensions
{
    public static IServiceCollection AddGupyIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GupyOptions>()
            .Bind(configuration.GetSection(GupyOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<GupyPacing>();
        services.AddTransient<GupyPacingHandler>();

        var http = services.AddHttpClient<GupySearchClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<GupyOptions>>().Value;
            client.BaseAddress = o.BaseAddress;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR");
        });
        // Retries 429 and 5xx with exponential backoff, honouring Retry-After.
        http.AddStandardResilienceHandler(r =>
        {
            r.Retry.MaxRetryAttempts = 3;
            r.Retry.Delay = TimeSpan.FromSeconds(5);
            r.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
            r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            r.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
        });
        // Inside the resilience handler: retries go through the budget too.
        http.AddHttpMessageHandler<GupyPacingHandler>();

        services.AddTransient<IJobSource, GupyJobSource>();
        return services;
    }
}

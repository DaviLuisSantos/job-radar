using JobRadar.Application.Abstractions;
using JobRadar.Integrations.Seek.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek;

public static class SeekServiceCollectionExtensions
{
    public static IServiceCollection AddSeekIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SeekOptions>()
            .Bind(configuration.GetSection(SeekOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        services.AddHttpClient<SeekSearchClient>((sp, http) =>
            {
                var o = sp.GetRequiredService<IOptions<SeekOptions>>().Value;
                http.BaseAddress = o.BaseAddress;
                http.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
                http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-AU");
            })
            // Retries 429 and 5xx with exponential backoff, honouring Retry-After.
            .AddStandardResilienceHandler(r =>
            {
                r.Retry.MaxRetryAttempts = 3;
                r.Retry.Delay = TimeSpan.FromSeconds(5);
                r.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                r.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
            });

        services.AddTransient<IJobSource, SeekJobSource>();
        services.AddTransient<IFullTextSearch, SeekFullTextSearch>();
        return services;
    }
}

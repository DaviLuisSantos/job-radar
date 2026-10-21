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
        services.AddSingleton<SeekPacing>();
        services.AddTransient<SeekPacingHandler>();

        var http = services.AddHttpClient<SeekSearchClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<SeekOptions>>().Value;
            client.BaseAddress = o.BaseAddress;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-AU");
        });

        // Retries 429 and 5xx with exponential backoff, honouring Retry-After.
        http.AddStandardResilienceHandler(r =>
        {
            r.Retry.MaxRetryAttempts = 3;
            r.Retry.Delay = TimeSpan.FromSeconds(5);
            // An attempt includes its wait in the pacing queue (seconds at most): room for both.
            r.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
            r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);   // at least twice the attempt timeout
            r.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
        });

        // Registered after the resilience handler, so it sits inside it: every attempt, retries included,
        // goes through the one budget shared by the whole process.
        http.AddHttpMessageHandler<SeekPacingHandler>();

        services.AddTransient<IJobSource, SeekJobSource>();
        services.AddTransient<IFullTextSearch, SeekFullTextSearch>();
        services.AddTransient<SeekJobSource>();
        services.AddTransient<IMarketSource, SeekMarketSource>();
        return services;
    }
}

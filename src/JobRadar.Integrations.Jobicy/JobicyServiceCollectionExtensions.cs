using JobRadar.Application.Abstractions;
using JobRadar.Integrations.Jobicy.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Jobicy;

public static class JobicyServiceCollectionExtensions
{
    public static IServiceCollection AddJobicyIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobicyOptions>()
            .Bind(configuration.GetSection(JobicyOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<JobicyPacing>();
        services.AddTransient<JobicyPacingHandler>();

        var http = services.AddHttpClient<JobicySearchClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<JobicyOptions>>().Value;
            client.BaseAddress = o.BaseAddress;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
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
        http.AddHttpMessageHandler<JobicyPacingHandler>();

        services.AddTransient<IJobSource, JobicyJobSource>();
        return services;
    }
}

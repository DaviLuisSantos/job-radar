using System.Net;
using JobRadar.Application.Abstractions;
using JobRadar.Integrations.LinkedIn.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.LinkedIn;

public static class LinkedInServiceCollectionExtensions
{
    /// <summary>The status LinkedIn answers a client it has stopped serving.</summary>
    private const HttpStatusCode Refused = (HttpStatusCode)999;

    public static IServiceCollection AddLinkedInIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LinkedInOptions>()
            .Bind(configuration.GetSection(LinkedInOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<LinkedInPacing>();
        services.AddTransient<LinkedInPacingHandler>();

        // Search and ad text share one client, so one request budget.
        var http = services.AddHttpClient<LinkedInGuestClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<LinkedInOptions>>().Value;
            client.BaseAddress = o.BaseAddress;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(o.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            // English whatever the market: the translator reads the country names the cards end with.
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
        });
        // Retries 429 and 5xx with exponential backoff, honouring Retry-After; not 999, LinkedIn's refusal, which a
        // retry only confirms (the client stops on it).
        http.AddStandardResilienceHandler(r =>
        {
            r.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result?.StatusCode != Refused && HttpClientResiliencePredicates.IsTransient(args.Outcome));
            r.Retry.MaxRetryAttempts = 3;
            r.Retry.Delay = TimeSpan.FromSeconds(5);
            r.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
            r.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            r.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
        });
        // Inside the resilience handler: retries go through the budget too.
        http.AddHttpMessageHandler<LinkedInPacingHandler>();

        services.AddTransient<IJobSource, LinkedInJobSource>();
        services.AddTransient<IDescriptionSource, LinkedInDescriptionSource>();
        return services;
    }
}

using JobRadar.Application.Collection;
using JobRadar.Application.Market;
using JobRadar.Application.Matching;
using JobRadar.Application.Tracking;
using JobRadar.Application.Triage;
using JobRadar.Domain.Matching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobRadar.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // Services read the clock through TimeProvider so tests can pin "now".
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<CollectionOptions>()
            .Bind(configuration.GetSection(CollectionOptions.Section))
            .ValidateDataAnnotations()
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Unknown Collection:TimeZone.")
            .ValidateOnStart();

        services.AddScoped<ListingCollector>();

        services.AddOptions<MatchingOptions>().Bind(configuration.GetSection(MatchingOptions.Section));
        // The defaults are the calibrated weights; overriding them is a code change, on purpose.
        services.TryAddSingleton(ScoringPolicy.Default);
        services.AddSingleton<ProfileCatalog>();
        services.AddScoped<ListingScoringService>();
        services.AddScoped<FullTextTermService>();
        services.AddScoped<TrackingService>();
        services.AddScoped<CalibrationService>();
        services.AddScoped<TriageService>();

        services.AddOptions<MarketOptions>().Bind(configuration.GetSection(MarketOptions.Section));
        services.AddSingleton<Microsoft.Extensions.Options.IPostConfigureOptions<MarketOptions>, MarketDefaults>();
        services.AddScoped<MarketSnapshotService>();
        services.AddScoped<MarketReportService>();
        return services;
    }
}

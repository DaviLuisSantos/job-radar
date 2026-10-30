using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Gupy.Api;

/// <summary>
/// One budget for every request this process sends to Gupy: at most <see cref="GupyOptions.MaxConcurrentRequests"/>
/// in flight and <see cref="GupyOptions.RequestsPerSecond"/> started per second. Inside the resilience pipeline,
/// so retries are paced too.
/// </summary>
internal sealed class GupyPacing : IDisposable
{
    public GupyPacing(IOptions<GupyOptions> options)
    {
        var o = options.Value;
        Concurrency = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = o.MaxConcurrentRequests,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
        Rate = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 1,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1.0 / o.RequestsPerSecond),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    public ConcurrencyLimiter Concurrency { get; }
    public TokenBucketRateLimiter Rate { get; }

    public void Dispose()
    {
        Concurrency.Dispose();
        Rate.Dispose();
    }
}

internal sealed class GupyPacingHandler(GupyPacing pacing) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var slot = await pacing.Concurrency.AcquireAsync(1, ct);
        using var token = await pacing.Rate.AcquireAsync(1, ct);
        return await base.SendAsync(request, ct);
    }
}

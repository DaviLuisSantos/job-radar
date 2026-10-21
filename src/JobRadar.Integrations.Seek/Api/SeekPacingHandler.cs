using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek.Api;

/// <summary>
/// One budget for every request this process sends to Seek, whatever stage sends it: at most
/// <see cref="SeekOptions.MaxConcurrentRequests"/> in flight and <see cref="SeekOptions.RequestsPerSecond"/>
/// started per second. Callers can run in parallel freely; this keeps the total polite. It sits inside
/// the resilience pipeline, so retries are paced too.
/// </summary>
internal sealed class SeekPacing : IDisposable
{
    public SeekPacing(IOptions<SeekOptions> options)
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
            // One token per 1/RPS seconds, no burst: requests start evenly spread.
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

    // Synchronous on purpose: a container disposed with Dispose() (not DisposeAsync) must not throw.
    public void Dispose()
    {
        Concurrency.Dispose();
        Rate.Dispose();
    }
}

internal sealed class SeekPacingHandler(SeekPacing pacing) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var slot = await pacing.Concurrency.AcquireAsync(1, ct);
        using var token = await pacing.Rate.AcquireAsync(1, ct);
        return await base.SendAsync(request, ct);
    }
}

using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.LinkedIn.Api;

/// <summary>
/// One budget for every request this process sends to LinkedIn, search and ad text alike: at most
/// <see cref="LinkedInOptions.MaxConcurrentRequests"/> in flight and <see cref="LinkedInOptions.RequestsPerSecond"/>
/// started per second. Inside the resilience pipeline, so retries are paced too.
/// </summary>
internal sealed class LinkedInPacing : IDisposable
{
    public LinkedInPacing(IOptions<LinkedInOptions> options)
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

    private readonly Lock _gate = new();
    private (DateTimeOffset Until, string Reason)? _blocked;

    /// <summary>
    /// After a refusal, every request until <paramref name="until"/> is refused here, without asking LinkedIn: the
    /// collector runs every search even when one fails, and pushing on is what keeps an address blocked.
    /// </summary>
    public void Block(DateTimeOffset until, string reason)
    {
        lock (_gate)
            _blocked = (until, reason);
    }

    /// <summary>Why requests are refused at <paramref name="now"/>; null when they may go.</summary>
    public string? BlockedReason(DateTimeOffset now)
    {
        lock (_gate)
            return _blocked is { } b && now < b.Until ? b.Reason : null;
    }

    public void Dispose()
    {
        Concurrency.Dispose();
        Rate.Dispose();
    }
}

internal sealed class LinkedInPacingHandler(LinkedInPacing pacing) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var slot = await pacing.Concurrency.AcquireAsync(1, ct);
        using var token = await pacing.Rate.AcquireAsync(1, ct);
        return await base.SendAsync(request, ct);
    }
}

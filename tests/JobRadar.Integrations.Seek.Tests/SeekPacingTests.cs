using System.Diagnostics;
using System.Net;
using JobRadar.Integrations.Seek.Api;
using Microsoft.Extensions.Options;

namespace JobRadar.Integrations.Seek.Tests;

public class SeekPacingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Counts how many requests are inside at once.</summary>
    private sealed class SlowServer(TimeSpan hold) : HttpMessageHandler
    {
        private int _inFlight;
        public int Peak;
        public int Served;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var now = Interlocked.Increment(ref _inFlight);
            for (var peak = Peak; now > peak; peak = Peak)
                Interlocked.CompareExchange(ref Peak, now, peak);
            await Task.Delay(hold, ct);
            Interlocked.Decrement(ref _inFlight);
            Interlocked.Increment(ref Served);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static (HttpClient Client, SlowServer Server) Paced(int maxConcurrent, double perSecond, TimeSpan hold)
    {
        var server = new SlowServer(hold);
        var pacing = new SeekPacing(Options.Create(new SeekOptions { MaxConcurrentRequests = maxConcurrent, RequestsPerSecond = perSecond }));
        var client = new HttpClient(new SeekPacingHandler(pacing) { InnerHandler = server }) { BaseAddress = new Uri("https://example.test/") };
        return (client, server);
    }

    [Fact]
    public async Task Never_more_requests_in_flight_than_allowed()
    {
        var (client, server) = Paced(maxConcurrent: 3, perSecond: 1000, hold: TimeSpan.FromMilliseconds(100));

        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.GetAsync("x", Ct)));

        Assert.Equal(12, server.Served);
        Assert.Equal(3, server.Peak);
    }

    [Fact]
    public async Task Requests_start_no_faster_than_the_rate()
    {
        var (client, server) = Paced(maxConcurrent: 16, perSecond: 20, hold: TimeSpan.Zero);
        var watch = Stopwatch.StartNew();

        await Task.WhenAll(Enumerable.Range(0, 11).Select(_ => client.GetAsync("x", Ct)));

        // 11 requests at 20/s: the last starts no sooner than 10 × 50 ms after the first.
        Assert.Equal(11, server.Served);
        Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(450), $"took {watch.Elapsed.TotalMilliseconds:N0} ms");
    }
}

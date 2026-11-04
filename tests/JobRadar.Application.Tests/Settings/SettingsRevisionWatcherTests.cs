using JobRadar.Application.Abstractions;
using JobRadar.Application.Settings;
using JobRadar.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace JobRadar.Application.Tests.Settings;

public class SettingsRevisionWatcherTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero));
    private readonly Revisions _repository = new() { Current = "1|2026-11-03T00:00:00.0000000Z" };

    private SettingsRevisionWatcher Watcher(string? loaded = "1|2026-11-03T00:00:00.0000000Z") =>
        new(new Scopes(_repository), _time, loaded);

    /// <summary>Advances one slice and waits until the watcher has looked, then until it armed its next delay.</summary>
    private async Task TickAsync()
    {
        var calls = _repository.Calls;
        _time.Advance(SettingsRevisionWatcher.Slice);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (_repository.Calls == calls && DateTime.UtcNow < deadline)
            await Task.Delay(5, Ct);
        await Task.Delay(30, Ct);
    }

    [Fact]
    public async Task Nothing_changed_keeps_waiting_and_a_change_ends_the_wait()
    {
        var wait = Watcher().WaitForChangeAsync(TimeSpan.FromMinutes(30), Ct);

        await TickAsync();
        await TickAsync();
        Assert.False(wait.IsCompleted);

        _repository.Current = "2|2026-11-03T00:02:00.0000000Z";
        await TickAsync();

        Assert.True(await wait.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task The_wait_ends_false_when_the_time_runs_out_without_a_change()
    {
        var wait = Watcher().WaitForChangeAsync(TimeSpan.FromSeconds(90), Ct);

        await TickAsync();
        Assert.False(wait.IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(30));

        Assert.False(await wait.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task A_database_that_cannot_be_read_is_not_a_change()
    {
        _repository.Fail = true;
        var wait = Watcher().WaitForChangeAsync(TimeSpan.FromMinutes(30), Ct);

        await TickAsync();
        await TickAsync();
        Assert.False(wait.IsCompleted);

        _repository.Fail = false;
        _repository.Current = "3|2026-11-03T00:05:00.0000000Z";
        await TickAsync();
        Assert.True(await wait.WaitAsync(TimeSpan.FromSeconds(5), Ct));
    }

    [Fact]
    public async Task A_process_that_never_read_the_table_matches_an_empty_table()
    {
        _repository.Current = "0|";
        var watcher = Watcher(loaded: null);

        Assert.Equal("0|", watcher.Loaded);
        Assert.False(await watcher.HasChangedAsync(Ct));

        _repository.Current = "1|2026-11-03T00:00:00.0000000Z";
        Assert.True(await watcher.HasChangedAsync(Ct));
    }

    private sealed class Revisions : ISettingsRepository
    {
        private int _calls;

        public string Current { get; set; } = "0|";
        public bool Fail { get; set; }
        public int Calls => Volatile.Read(ref _calls);

        public Task<string> GetRevisionAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            return Fail ? throw new InvalidOperationException("database down") : Task.FromResult(Current);
        }

        public Task<IReadOnlyList<Setting>> ListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Setting?> GetAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(string key, string json, DateTimeOffset now, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Scopes(ISettingsRepository repository) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(repository);

        private sealed class Scope(ISettingsRepository repository) : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;
            public object? GetService(Type serviceType) => serviceType == typeof(ISettingsRepository) ? repository : null;
            public void Dispose() { }
        }
    }
}

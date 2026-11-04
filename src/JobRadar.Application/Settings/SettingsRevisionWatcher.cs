using JobRadar.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Settings;

/// <summary>
/// Configuration is read once at start, so a process notices a change in the stored sections by comparing the
/// revision it loaded (<see cref="ConfigurationKey"/>) with the table's, in slices of <see cref="Slice"/>, and asks
/// its host to restart. Never called while a pass runs: only while idle.
/// </summary>
public sealed class SettingsRevisionWatcher(
    IServiceScopeFactory scopes,
    TimeProvider time,
    string? loadedRevision,
    ILogger? log = null)
{
    /// <summary>Set by the database configuration source; same format as <see cref="ISettingsRepository.GetRevisionAsync"/>.</summary>
    public const string ConfigurationKey = "JobRadar:SettingsRevision";

    /// <summary>The revision of an empty table, and of a process that did not read the table at all.</summary>
    public const string EmptyRevision = "0|";

    public static readonly TimeSpan Slice = TimeSpan.FromSeconds(60);

    public string Loaded { get; } = string.IsNullOrEmpty(loadedRevision) ? EmptyRevision : loadedRevision;

    /// <summary>
    /// Waits up to <paramref name="max"/> (forever for <see cref="Timeout.InfiniteTimeSpan"/>), checking every
    /// <see cref="Slice"/>. True as soon as the stored revision differs from the loaded one; false when the time ran out.
    /// </summary>
    public async Task<bool> WaitForChangeAsync(TimeSpan max, CancellationToken ct)
    {
        var forever = max == Timeout.InfiniteTimeSpan;
        var remaining = max;
        while (forever || remaining > TimeSpan.Zero)
        {
            var step = forever || remaining > Slice ? Slice : remaining;
            await Task.Delay(step, time, ct);
            remaining -= step;
            if (await HasChangedAsync(ct))
                return true;
        }
        return false;
    }

    /// <summary>A database that cannot be reached right now is not a change: the next slice asks again.</summary>
    public async Task<bool> HasChangedAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var current = await scope.ServiceProvider.GetRequiredService<ISettingsRepository>().GetRevisionAsync(ct);
            return current != Loaded;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log?.LogWarning(e, "Could not read the settings revision; will try again");
            return false;
        }
    }
}

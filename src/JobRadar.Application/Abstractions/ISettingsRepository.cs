using JobRadar.Domain.Settings;

namespace JobRadar.Application.Abstractions;

/// <summary>Configuration sections stored in the database, one row per section key.</summary>
public interface ISettingsRepository
{
    Task<IReadOnlyList<Setting>> ListAsync(CancellationToken ct = default);

    Task<Setting?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Inserts or replaces the section, stamping it with <paramref name="now"/>.</summary>
    Task SaveAsync(string key, string json, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>True when the section existed.</summary>
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// <c>"{count}|{maxUpdatedAtUtc:O}"</c> (same as the <c>JobRadar:SettingsRevision</c> configuration key); empty is <c>"0|"</c>.
    /// </summary>
    Task<string> GetRevisionAsync(CancellationToken ct = default);
}

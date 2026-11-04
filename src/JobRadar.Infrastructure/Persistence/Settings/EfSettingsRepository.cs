using JobRadar.Application.Abstractions;
using JobRadar.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Infrastructure.Persistence.Settings;

internal sealed class EfSettingsRepository(JobRadarDbContext db) : ISettingsRepository
{
    public async Task<IReadOnlyList<Setting>> ListAsync(CancellationToken ct = default)
    {
        var rows = await db.Settings.OrderBy(s => s.Key).ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<Setting?> GetAsync(string key, CancellationToken ct = default)
    {
        var row = await db.Settings.SingleOrDefaultAsync(s => s.Key == key, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task SaveAsync(string key, string json, DateTimeOffset now, CancellationToken ct = default)
    {
        try
        {
            var row = await db.Settings.AsTracking().SingleOrDefaultAsync(s => s.Key == key, ct);
            if (row is null)
                db.Settings.Add(new SettingEntity { Key = key, Value = json, UpdatedAt = now.ToUniversalTime() });
            else
            {
                row.Value = json;
                row.UpdatedAt = now.ToUniversalTime();
            }
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default) =>
        await db.Settings.Where(s => s.Key == key).ExecuteDeleteAsync(ct) > 0;

    public async Task<string> GetRevisionAsync(CancellationToken ct = default)
    {
        var stamps = await db.Settings.Select(s => s.UpdatedAt).ToListAsync(ct);
        return Revision(stamps);
    }

    /// <summary>In memory: the table is a handful of rows, and max over datetimeoffset differs by database.</summary>
    internal static string Revision(IReadOnlyCollection<DateTimeOffset> updatedAt) =>
        updatedAt.Count == 0 ? "0|" : $"{updatedAt.Count}|{updatedAt.Max().UtcDateTime:O}";

    internal static Setting ToDomain(SettingEntity e) => new(e.Key, e.Value, e.UpdatedAt);
}

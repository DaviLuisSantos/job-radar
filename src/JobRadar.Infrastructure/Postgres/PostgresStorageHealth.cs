using JobRadar.Application.Abstractions;

namespace JobRadar.Infrastructure.Postgres;

internal sealed class PostgresStorageHealth(JobRadarDbContext db) : IStorageHealth
{
    public async Task<bool> CanConnectAsync(CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}

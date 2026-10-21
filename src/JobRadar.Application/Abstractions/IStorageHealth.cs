namespace JobRadar.Application.Abstractions;

/// <summary>
/// Whether the database answers. On a PC the worker service can start before Docker does, so
/// the pipeline waits for this instead of failing its first pass.
/// </summary>
public interface IStorageHealth
{
    Task<bool> CanConnectAsync(CancellationToken ct = default);
}

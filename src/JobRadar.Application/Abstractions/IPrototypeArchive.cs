using JobRadar.Domain.Import;

namespace JobRadar.Application.Abstractions;

/// <summary>Reads the Python prototype store (its <c>jobs.db</c>), translated to neutral records.</summary>
public interface IPrototypeArchive
{
    Task<PrototypeData> ReadAsync(string path, CancellationToken ct = default);
}

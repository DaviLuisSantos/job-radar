namespace JobRadar.Web;

/// <summary>
/// Runs one operation in its own DI scope. In interactive Blazor a scoped service lives as long
/// as the browser tab (the circuit): one DbContext would be shared by every click, go stale and
/// break on concurrent events. Each call here gets a fresh scope, and so a fresh DbContext.
/// </summary>
public sealed class Backend(IServiceScopeFactory scopes)
{
    public async Task<T> RunAsync<TService, T>(Func<TService, Task<T>> work) where TService : notnull
    {
        await using var scope = scopes.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public async Task RunAsync<TService>(Func<TService, Task> work) where TService : notnull
    {
        await using var scope = scopes.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<TService>());
    }
}

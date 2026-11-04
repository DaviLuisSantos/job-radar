using JobRadar.Application.Settings;

namespace JobRadar.Web;

/// <summary>
/// <c>/api/config</c>: reads and writes the configuration kept in the database. No authentication of its own: the site is
/// reachable only on localhost and the private network.
/// </summary>
public static class ConfigEndpoints
{
    public static IEndpointRouteBuilder MapConfigApi(this IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api/config");
        api.MapGet("", GetEffective);
        api.MapGet("{key}", Get);
        api.MapPut("{key}", Put);
        api.MapDelete("{key}", Delete);
        return routes;
    }

    internal static IResult GetEffective(SettingsService settings) =>
        Results.Content(settings.GetEffective(), "application/json");

    internal static async Task<IResult> Get(string key, SettingsService settings, CancellationToken ct)
    {
        if (!IsAllowed(key))
            return Forbidden(key);
        var setting = await settings.GetAsync(key, ct);
        return setting is null ? Results.NotFound() : Results.Content(setting.Json, "application/json");
    }

    internal static async Task<IResult> Put(string key, HttpRequest request, SettingsService settings, CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var json = await reader.ReadToEndAsync(ct);
        try
        {
            await settings.SaveAsync(key, json, ct);
            return Results.NoContent();
        }
        catch (SettingsValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    internal static async Task<IResult> Delete(string key, SettingsService settings, CancellationToken ct)
    {
        try
        {
            return await settings.DeleteAsync(key, ct) ? Results.NoContent() : Results.NotFound();
        }
        catch (SettingsValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static bool IsAllowed(string key) =>
        SettingsService.AllowedPrefixes.Contains(key.Split(':')[0], StringComparer.OrdinalIgnoreCase);

    private static IResult Forbidden(string key) =>
        Results.BadRequest(new { error = $"Key '{key}' is not allowed. Use a key starting with: {string.Join(", ", SettingsService.AllowedPrefixes)}." });
}

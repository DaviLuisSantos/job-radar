using System.Text;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Settings;
using JobRadar.Domain.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace JobRadar.Web.Tests;

public class ConfigEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeSettings _repository = new();

    private SettingsService Service() => new(_repository, new ConfigurationBuilder().Build(), TimeProvider.System);

    private static HttpRequest Body(string json)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return context.Request;
    }

    [Fact]
    public async Task Put_saves_and_get_returns_it()
    {
        Assert.IsType<NoContent>(await ConfigEndpoints.Put("Market:Terms", Body("""{"x":1}"""), Service(), Ct));
        var got = Assert.IsType<ContentHttpResult>(await ConfigEndpoints.Get("Market:Terms", Service(), Ct));
        Assert.Equal("""{"x":1}""", got.ResponseContent);
    }

    [Fact]
    public async Task Get_of_a_missing_key_is_404() =>
        Assert.IsType<NotFound>(await ConfigEndpoints.Get("Market:Terms", Service(), Ct));

    [Fact]
    public async Task A_forbidden_key_is_400()
    {
        var get = (IStatusCodeHttpResult)await ConfigEndpoints.Get("ConnectionStrings:JobRadar", Service(), Ct);
        Assert.Equal(400, get.StatusCode);
        var put = (IStatusCodeHttpResult)await ConfigEndpoints.Put("Database:Provider", Body("\"x\""), Service(), Ct);
        Assert.Equal(400, put.StatusCode);
    }

    [Fact]
    public async Task Invalid_json_is_400()
    {
        var put = (IStatusCodeHttpResult)await ConfigEndpoints.Put("Market:Terms", Body("{nope"), Service(), Ct);
        Assert.Equal(400, put.StatusCode);
        Assert.Empty(_repository.Rows);
    }

    [Fact]
    public async Task Delete_removes_or_says_404()
    {
        await ConfigEndpoints.Put("Market:Terms", Body("1"), Service(), Ct);
        Assert.IsType<NoContent>(await ConfigEndpoints.Delete("Market:Terms", Service(), Ct));
        Assert.IsType<NotFound>(await ConfigEndpoints.Delete("Market:Terms", Service(), Ct));
    }

    private sealed class FakeSettings : ISettingsRepository
    {
        public List<Setting> Rows { get; } = [];
        public Task<IReadOnlyList<Setting>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<Setting>>(Rows.ToList());
        public Task<Setting?> GetAsync(string key, CancellationToken ct) => Task.FromResult(Rows.FirstOrDefault(r => r.Key == key));
        public Task SaveAsync(string key, string json, DateTimeOffset now, CancellationToken ct)
        {
            Rows.RemoveAll(r => r.Key == key);
            Rows.Add(new Setting(key, json, now));
            return Task.CompletedTask;
        }
        public Task<bool> DeleteAsync(string key, CancellationToken ct) => Task.FromResult(Rows.RemoveAll(r => r.Key == key) > 0);
        public Task<string> GetRevisionAsync(CancellationToken ct) => Task.FromResult($"{Rows.Count}|");
    }
}

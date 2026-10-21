using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Worker.Tests;

public class CompositionTests
{
    private static ServiceProvider BuildAsInProgram()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Never opened: resolving services does not connect.
                ["ConnectionStrings:JobRadar"] = "Host=localhost;Database=unused",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobRadar(configuration);
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void Container_builds_with_no_missing_or_captive_dependency()
    {
        using var sp = BuildAsInProgram();
    }

    [Fact]
    public void Every_application_port_is_registered()
    {
        // ValidateOnBuild only checks what is registered; this catches a port nobody registered.
        using var sp = BuildAsInProgram();
        using var scope = sp.CreateScope();

        var ports = typeof(IListingRepository).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace == typeof(IListingRepository).Namespace);

        Assert.All(ports, port => Assert.NotNull(scope.ServiceProvider.GetService(port)));
    }
}

using ApiGateway.RateLimiting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ApiGateway.UnitTests;

/// <summary>
/// Tjekker gatewayens rigtige appsettings.json. En stavefejl i et cluster-navn opdages ellers først,
/// når gatewayen starter i Docker.
/// </summary>
public class GatewayConfigurationTests
{
    private static readonly IConfiguration Proxy = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "gateway.appsettings.json"))
        .Build()
        .GetSection("ReverseProxy");

    private static IEnumerable<IConfigurationSection> Routes => Proxy.GetSection("Routes").GetChildren();

    [Fact]
    public void Every_route_points_at_a_cluster_that_exists()
    {
        var clusters = Proxy.GetSection("Clusters").GetChildren().Select(c => c.Key).ToHashSet();

        foreach (var route in Routes)
        {
            Assert.Contains(route["ClusterId"], clusters);
        }
    }

    [Fact]
    public void Every_route_has_a_known_rate_limit_policy()
    {
        var policies = new[] { GatewayRateLimiting.Web, GatewayRateLimiting.Api, GatewayRateLimiting.Login };

        foreach (var route in Routes)
        {
            Assert.Contains(route["RateLimiterPolicy"], policies);
        }
    }

    [Fact]
    public void Writing_to_the_catalog_requires_the_admin_policy()
    {
        var writes = Routes.Single(r => r.Key == "catalog-items-write");

        Assert.Equal("admin", writes["AuthorizationPolicy"]);
    }

    [Fact]
    public void The_catalog_cluster_balances_two_health_checked_instances()
    {
        var catalog = Proxy.GetSection("Clusters:catalog");

        Assert.Equal("RoundRobin", catalog["LoadBalancingPolicy"]);
        Assert.Equal("true", catalog["HealthCheck:Active:Enabled"]);
        Assert.Equal("/health", catalog["HealthCheck:Active:Path"]);
        Assert.Equal(2, catalog.GetSection("Destinations").GetChildren().Count());
    }
}

using ApiGateway.Strangler;
using Microsoft.Extensions.Configuration;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.UnitTests;

public class CatalogStranglerFilterTests
{
    private static readonly RouteConfig CatalogRoute = new()
    {
        RouteId = "catalog-brands",
        ClusterId = CatalogStranglerFilter.CatalogCluster,
        Match = new RouteMatch { Path = "/api/catalog-brands" }
    };

    private static readonly RouteConfig BasketRoute = new()
    {
        RouteId = "baskets",
        ClusterId = "basket",
        Match = new RouteMatch { Path = "/api/baskets/{**rest}" }
    };

    [Fact]
    public async Task With_the_flag_off_catalog_routes_go_to_the_monolith()
    {
        var filter = Filter(useCatalogService: false);

        var route = await filter.ConfigureRouteAsync(CatalogRoute, null, CancellationToken.None);

        Assert.Equal(CatalogStranglerFilter.MonolithCluster, route.ClusterId);
        Assert.Equal(CatalogRoute.Match.Path, route.Match.Path);
    }

    [Fact]
    public async Task With_the_flag_on_catalog_routes_go_to_Catalog_API()
    {
        var filter = Filter(useCatalogService: true);

        var route = await filter.ConfigureRouteAsync(CatalogRoute, null, CancellationToken.None);

        Assert.Equal(CatalogStranglerFilter.CatalogCluster, route.ClusterId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Other_routes_are_never_changed(bool useCatalogService)
    {
        var filter = Filter(useCatalogService);

        var route = await filter.ConfigureRouteAsync(BasketRoute, null, CancellationToken.None);

        Assert.Same(BasketRoute, route);
    }

    private static CatalogStranglerFilter Filter(bool useCatalogService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CatalogStranglerFilter.FlagName] = useCatalogService.ToString()
            })
            .Build();

        return new CatalogStranglerFilter(configuration);
    }
}

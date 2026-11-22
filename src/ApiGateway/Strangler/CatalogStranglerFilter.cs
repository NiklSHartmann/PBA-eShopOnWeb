using Yarp.ReverseProxy.Configuration;

namespace ApiGateway.Strangler;

/// <summary>
/// Strangler Fig i kanten. I appsettings.json peger katalog-ruterne (/api/catalog-items, -brands, -types) på
/// Catalog.API (cluster "catalog"). Så længe feature flaget UseCatalogService er false, sender filteret dem i stedet
/// til monolittens PublicApi. At flytte kataloget, eller at rulle tilbage, er derfor kun et flag og en genstart.
/// Web læser samme flag, så læsning og skrivning altid skifter i samme trin (se docs/gateway.md).
/// </summary>
public sealed class CatalogStranglerFilter : IProxyConfigFilter
{
    public const string FlagName = "UseCatalogService";
    public const string CatalogCluster = "catalog";
    public const string MonolithCluster = "publicapi";

    private readonly bool _useCatalogService;

    public CatalogStranglerFilter(IConfiguration configuration)
    {
        _useCatalogService = configuration.GetValue<bool>(FlagName);
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel) => new(cluster);

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        if (_useCatalogService || route.ClusterId != CatalogCluster)
        {
            return new(route);
        }

        // RouteConfig er en record: "with" laver en kopi, hvor kun ClusterId er ændret.
        return new(route with { ClusterId = MonolithCluster });
    }
}

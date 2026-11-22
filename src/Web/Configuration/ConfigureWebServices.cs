using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.Services;

namespace Microsoft.eShopWeb.Web.Configuration;

public static class ConfigureWebServices
{
    public static IServiceCollection AddWebServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediatR(cfg => 
            cfg.RegisterServicesFromAssembly(typeof(BasketViewModelService).Assembly));
        services.AddScoped<IBasketViewModelService, BasketViewModelService>();
        services.AddScoped<CatalogViewModelService>();
        services.AddScoped<ICatalogItemViewModelService, CatalogItemViewModelService>();

        // HttpClient for Ordering.API. Adresse is in appsettings (OrderingApi:BaseUrl).
        services.AddHttpClient<IOrderingApiClient, OrderingApiClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["OrderingApi:BaseUrl"] ?? "http://localhost:5300/");
            client.Timeout = TimeSpan.FromSeconds(5);
            // Timeout is set to 5 seconds. If the Ordering.API does not respond within this time, a timeout exception will be thrown.
        });

        // Branch by Abstraction for kataloget: feature flaget vælger, hvor ICatalogClient henter fra.
        // Gatewayen læser samme flag, så læsning og skrivning skifter i samme trin. Tilbage igen: sæt flaget til false.
        if (configuration.GetValue<bool>("UseCatalogService"))
        {
            services.AddHttpClient<ICatalogClient, CatalogApiClient>(client =>
            {
                client.BaseAddress = new Uri(configuration["CatalogApi:BaseUrl"] ?? "http://localhost:5600/");
                client.Timeout = TimeSpan.FromSeconds(5);
            });
        }
        else
        {
            services.AddScoped<ICatalogClient, LocalCatalogClient>(); // den gamle kode
        }

        services.Configure<CatalogSettings>(configuration);
        services.AddScoped<ICatalogViewModelService, CachedCatalogViewModelService>();

        return services;
    }
}

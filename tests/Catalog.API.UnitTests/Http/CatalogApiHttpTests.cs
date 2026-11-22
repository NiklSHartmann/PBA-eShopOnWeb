using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalog.API.Data;
using Catalog.API.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Catalog.API.UnitTests.Http;

/// <summary>
/// Starter hele Catalog.API i hukommelsen (WebApplicationFactory) med SQLite i stedet for SQL Server og kalder den
/// over HTTP. Testene låser JSON-formen fast: BlazorAdmin læser catalogItems, pageCount og catalogBrands,
/// præcis som fra PublicApi, og skrivning uden token skal afvises.
/// </summary>
public class CatalogApiHttpTests : IClassFixture<CatalogApiHttpTests.CatalogApiFactory>
{
    private readonly CatalogApiFactory _factory;

    public CatalogApiHttpTests(CatalogApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Brands_have_the_same_json_shape_as_PublicApi()
    {
        var response = await _factory.CreateClient().GetAsync("/api/catalog-brands");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Catalog-Instance"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var first = json.RootElement.GetProperty("catalogBrands")[0];
        Assert.Equal(1, first.GetProperty("id").GetInt32());
        Assert.Equal(".NET", first.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Items_are_paged_like_PublicApi()
    {
        var response = await _factory.CreateClient().GetAsync("/api/catalog-items?pageSize=2&pageIndex=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("catalogItems").GetArrayLength());
        Assert.Equal(2, json.RootElement.GetProperty("pageCount").GetInt32());
        Assert.Equal(3, json.RootElement.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task Creating_an_item_without_a_token_is_rejected()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/catalog-items",
            new { name = "Sneaky mug", description = "No token", price = 1m, catalogBrandId = 1, catalogTypeId = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_healthy_when_the_tables_exist()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public sealed class CatalogApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();

            // Ingen migrations og ingen kopi fra monolitten i testen; tabellerne laves med EnsureCreated nedenfor.
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Catalog:InitializeDatabase"] = "false" }));

            builder.ConfigureServices(services =>
            {
                var sqlServer = services.Single(d => d.ServiceType == typeof(DbContextOptions<CatalogDbContext>));
                services.Remove(sqlServer);
                services.AddDbContext<CatalogDbContext>(options => options.UseSqlite(_connection));

                using var scope = services.BuildServiceProvider().CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
                db.Database.EnsureCreated();
                db.CatalogBrands.AddRange(new CatalogBrand(".NET"), new CatalogBrand("Azure"));
                db.CatalogTypes.Add(new CatalogType("Mug"));
                db.SaveChanges();
                db.CatalogItems.AddRange(
                    new CatalogItem(1, 1, "Mug one", "Mug one", 8m, "http://catalogbaseurltobereplaced/images/products/1.png"),
                    new CatalogItem(1, 1, "Mug two", "Mug two", 9m, "http://catalogbaseurltobereplaced/images/products/2.png"),
                    new CatalogItem(1, 2, "Mug three", "Mug three", 10m, "http://catalogbaseurltobereplaced/images/products/3.png"));
                db.SaveChanges();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }
}

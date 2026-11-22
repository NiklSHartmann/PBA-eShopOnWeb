using Catalog.API.Data;
using Catalog.API.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.UnitTests;

/// <summary>
/// En katalogdatabase i hukommelsen (SQLite, ikke EF InMemory), så nøgler og fremmednøgler opfører sig
/// som i SQL Server. Databasen findes kun, så længe forbindelsen er åben.
/// </summary>
public sealed class TestCatalogDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private TestCatalogDatabase(SqliteConnection connection)
    {
        _connection = connection;
    }

    public static async Task<TestCatalogDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var database = new TestCatalogDatabase(connection);
        await using var db = database.NewContext();
        await db.Database.EnsureCreatedAsync();
        return database;
    }

    public CatalogDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(_connection).Options);

    /// <summary>To mærker, to typer og tre varer. Id'erne bliver 1, 2 og 1, 2, 3.</summary>
    public async Task SeedAsync()
    {
        await using var db = NewContext();
        db.CatalogBrands.AddRange(new CatalogBrand(".NET"), new CatalogBrand("Azure"));
        db.CatalogTypes.AddRange(new CatalogType("Mug"), new CatalogType("T-Shirt"));
        await db.SaveChangesAsync();

        db.CatalogItems.AddRange(
            new CatalogItem(1, 1, ".NET Bot Black Sweatshirt", ".NET Bot Black Sweatshirt", 19.5m, "http://catalogbaseurltobereplaced/images/products/1.png"),
            new CatalogItem(1, 1, ".NET Black & White Mug", ".NET Black & White Mug", 8.5m, "http://catalogbaseurltobereplaced/images/products/2.png"),
            new CatalogItem(2, 2, "Azure Shirt", "Azure Shirt", 12m, "http://catalogbaseurltobereplaced/images/products/3.png"));
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

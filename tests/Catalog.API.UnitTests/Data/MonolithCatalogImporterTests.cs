using Catalog.API.Data;
using Catalog.API.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Catalog.API.UnitTests.Data;

/// <summary>
/// Kopien fra monolitten: id'erne skal bevares (kurve, ordrer og lager bruger dem), og der kopieres kun én gang.
/// SQL Server-delen (IDENTITY_INSERT) kan ikke testes med SQLite; den prøves af, når Docker starter catalogapi-1.
/// </summary>
public class MonolithCatalogImporterTests : IAsyncLifetime
{
    private TestCatalogDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestCatalogDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task An_empty_catalog_gets_a_copy_with_the_same_ids()
    {
        var monolith = new FakeMonolith(new MonolithCatalogSnapshot(
            new[] { CatalogBrand.CopyFromMonolith(4, "SQL Server") },
            new[] { CatalogType.CopyFromMonolith(3, "Sheet") },
            new[] { CatalogItem.CopyFromMonolith(12, 3, 4, "Prism White TShirt", "Prism White TShirt", 12m, "pic.png") }));

        await using (var db = _database.NewContext())
        {
            await new MonolithCatalogImporter(db, monolith, NullLogger<MonolithCatalogImporter>.Instance)
                .ImportIfEmptyAsync(CancellationToken.None);
        }

        await using var check = _database.NewContext();
        var item = await check.CatalogItems.SingleAsync();
        Assert.Equal(12, item.Id);
        Assert.Equal(4, item.CatalogBrandId);
        Assert.Equal(4, (await check.CatalogBrands.SingleAsync()).Id);
        Assert.Equal(3, (await check.CatalogTypes.SingleAsync()).Id);
    }

    [Fact]
    public async Task A_catalog_with_data_is_left_alone()
    {
        await _database.SeedAsync();
        var monolith = new FakeMonolith(new MonolithCatalogSnapshot(
            Array.Empty<CatalogBrand>(), Array.Empty<CatalogType>(), Array.Empty<CatalogItem>()));

        await using (var db = _database.NewContext())
        {
            await new MonolithCatalogImporter(db, monolith, NullLogger<MonolithCatalogImporter>.Instance)
                .ImportIfEmptyAsync(CancellationToken.None);
        }

        Assert.False(monolith.WasRead);
        await using var check = _database.NewContext();
        Assert.Equal(3, await check.CatalogItems.CountAsync());
    }

    [Fact]
    public async Task An_empty_monolith_is_an_error_so_the_copy_is_tried_again()
    {
        var monolith = new FakeMonolith(new MonolithCatalogSnapshot(
            Array.Empty<CatalogBrand>(), Array.Empty<CatalogType>(), Array.Empty<CatalogItem>()));

        await using var db = _database.NewContext();
        var importer = new MonolithCatalogImporter(db, monolith, NullLogger<MonolithCatalogImporter>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ImportIfEmptyAsync(CancellationToken.None));
    }

    private sealed class FakeMonolith : IMonolithCatalogSource
    {
        private readonly MonolithCatalogSnapshot _snapshot;

        public FakeMonolith(MonolithCatalogSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public bool WasRead { get; private set; }

        public Task<MonolithCatalogSnapshot> ReadAsync(CancellationToken cancellationToken)
        {
            WasRead = true;
            return Task.FromResult(_snapshot);
        }
    }
}

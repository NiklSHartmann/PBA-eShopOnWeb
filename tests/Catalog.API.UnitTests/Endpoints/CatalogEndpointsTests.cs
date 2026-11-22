using Catalog.API.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Catalog.API.UnitTests.Endpoints;

/// <summary>
/// Kalder endpoint-metoderne direkte med en SQLite-database i hukommelsen. Svarene skal have samme form
/// som PublicApi's, ellers virker BlazorAdmin ikke, når gatewayen skifter til Catalog.API.
/// </summary>
public class CatalogEndpointsTests : IAsyncLifetime
{
    private static readonly PictureUriComposer Pictures = new(string.Empty);
    private TestCatalogDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = await TestCatalogDatabase.CreateAsync();
        await _database.SeedAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Listing_pages_the_items_and_counts_pages()
    {
        await using var db = _database.NewContext();

        var result = await CatalogEndpoints.ListItems(pageSize: 2, pageIndex: 1, null, null, null, db, Pictures, CancellationToken.None);

        var item = Assert.Single(result.Value!.CatalogItems);
        Assert.Equal(3, item.Id);
        Assert.Equal(2, result.Value.PageCount);
        Assert.Equal(3, result.Value.TotalItems);
    }

    [Fact]
    public async Task Listing_without_page_size_returns_everything_on_one_page()
    {
        await using var db = _database.NewContext();

        var result = await CatalogEndpoints.ListItems(null, null, null, null, null, db, Pictures, CancellationToken.None);

        Assert.Equal(3, result.Value!.CatalogItems.Count);
        Assert.Equal(1, result.Value.PageCount);
    }

    [Fact]
    public async Task Listing_filters_by_brand_and_by_ids()
    {
        await using var db = _database.NewContext();

        var byBrand = await CatalogEndpoints.ListItems(null, null, catalogBrandId: 2, null, null, db, Pictures, CancellationToken.None);
        var byIds = await CatalogEndpoints.ListItems(null, null, null, null, new[] { 1, 3 }, db, Pictures, CancellationToken.None);

        Assert.Equal(new[] { 3 }, byBrand.Value!.CatalogItems.Select(i => i.Id));
        Assert.Equal(new[] { 1, 3 }, byIds.Value!.CatalogItems.Select(i => i.Id));
    }

    [Fact]
    public async Task Picture_placeholder_is_replaced_before_the_item_is_sent()
    {
        await using var db = _database.NewContext();

        var result = await CatalogEndpoints.GetItem(1, db, Pictures, CancellationToken.None);

        var ok = Assert.IsType<Ok<GetByIdCatalogItemResponse>>(result.Result);
        Assert.Equal("/images/products/1.png", ok.Value!.CatalogItem.PictureUri);
    }

    [Fact]
    public async Task An_unknown_item_is_not_found()
    {
        await using var db = _database.NewContext();

        var result = await CatalogEndpoints.GetItem(99, db, Pictures, CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task Creating_an_item_stores_it_with_the_default_picture()
    {
        await using var db = _database.NewContext();
        var request = new CreateCatalogItemRequest { Name = "New mug", Description = "A new mug", Price = 5m, CatalogBrandId = 1, CatalogTypeId = 1 };

        var result = await CatalogEndpoints.CreateItem(request, db, Pictures, NullLoggerFactory.Instance, CancellationToken.None);

        var created = Assert.IsType<Created<CreateCatalogItemResponse>>(result.Result);
        Assert.Equal($"/api/catalog-items/{created.Value!.CatalogItem.Id}", created.Location);
        Assert.Equal("/images/products/eCatalog-item-default.png", created.Value.CatalogItem.PictureUri);
        await using var check = _database.NewContext();
        Assert.Equal(4, await check.CatalogItems.CountAsync());
    }

    [Fact]
    public async Task Creating_an_item_with_an_existing_name_is_a_conflict()
    {
        await using var db = _database.NewContext();
        var request = new CreateCatalogItemRequest { Name = "Azure Shirt", Description = "Again", Price = 5m, CatalogBrandId = 1, CatalogTypeId = 1 };

        var result = await CatalogEndpoints.CreateItem(request, db, Pictures, NullLoggerFactory.Instance, CancellationToken.None);

        var conflict = Assert.IsType<Conflict<ErrorResponse>>(result.Result);
        Assert.Equal(409, conflict.Value!.StatusCode);
    }

    [Fact]
    public async Task Creating_an_item_with_an_unknown_brand_or_a_zero_price_is_a_bad_request()
    {
        await using var db = _database.NewContext();
        var unknownBrand = new CreateCatalogItemRequest { Name = "X", Description = "X", Price = 5m, CatalogBrandId = 42, CatalogTypeId = 1 };
        var zeroPrice = new CreateCatalogItemRequest { Name = "Y", Description = "Y", Price = 0m, CatalogBrandId = 1, CatalogTypeId = 1 };

        var first = await CatalogEndpoints.CreateItem(unknownBrand, db, Pictures, NullLoggerFactory.Instance, CancellationToken.None);
        var second = await CatalogEndpoints.CreateItem(zeroPrice, db, Pictures, NullLoggerFactory.Instance, CancellationToken.None);

        Assert.IsType<BadRequest<ErrorResponse>>(first.Result);
        Assert.IsType<BadRequest<ErrorResponse>>(second.Result);
    }

    [Fact]
    public async Task Updating_an_item_changes_name_price_brand_and_type()
    {
        await using var db = _database.NewContext();
        var request = new UpdateCatalogItemRequest { Id = 2, Name = "Renamed mug", Description = "New text", Price = 9.75m, CatalogBrandId = 2, CatalogTypeId = 2 };

        var result = await CatalogEndpoints.UpdateItem(request, db, Pictures, NullLoggerFactory.Instance, CancellationToken.None);

        Assert.IsType<Ok<UpdateCatalogItemResponse>>(result.Result);
        await using var check = _database.NewContext();
        var stored = await check.CatalogItems.SingleAsync(i => i.Id == 2);
        Assert.Equal("Renamed mug", stored.Name);
        Assert.Equal(9.75m, stored.Price);
        Assert.Equal(2, stored.CatalogBrandId);
        Assert.Equal(2, stored.CatalogTypeId);
    }

    [Fact]
    public async Task Deleting_an_item_removes_it()
    {
        await using var db = _database.NewContext();

        var result = await CatalogEndpoints.DeleteItem(3, db, NullLoggerFactory.Instance, CancellationToken.None);

        var ok = Assert.IsType<Ok<DeleteCatalogItemResponse>>(result.Result);
        Assert.Equal("Deleted", ok.Value!.Status);
        await using var check = _database.NewContext();
        Assert.False(await check.CatalogItems.AnyAsync(i => i.Id == 3));
    }

    [Fact]
    public async Task Brands_and_types_are_returned_with_id_and_name()
    {
        await using var db = _database.NewContext();

        var brands = await CatalogEndpoints.ListBrands(db, CancellationToken.None);
        var types = await CatalogEndpoints.ListTypes(db, CancellationToken.None);

        Assert.Equal(new[] { ".NET", "Azure" }, brands.Value!.CatalogBrands.Select(b => b.Name));
        Assert.Equal(new[] { "Mug", "T-Shirt" }, types.Value!.CatalogTypes.Select(t => t.Name));
    }
}

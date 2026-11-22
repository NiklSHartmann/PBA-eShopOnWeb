using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

/// <summary>
/// Den gamle vej: kataloget læses og skrives i monolittens egen database, præcis som før.
/// Koden er flyttet hertil fra CatalogViewModelService, CatalogItemViewModelService, BasketViewModelService
/// og OrderService. Den slettes, når Catalog.API har kørt stabilt (issue #28).
/// </summary>
public class LocalCatalogClient : ICatalogClient
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IRepository<CatalogBrand> _brandRepository;
    private readonly IRepository<CatalogType> _typeRepository;
    private readonly IUriComposer _uriComposer;

    public LocalCatalogClient(IRepository<CatalogItem> itemRepository,
        IRepository<CatalogBrand> brandRepository,
        IRepository<CatalogType> typeRepository,
        IUriComposer uriComposer)
    {
        _itemRepository = itemRepository;
        _brandRepository = brandRepository;
        _typeRepository = typeRepository;
        _uriComposer = uriComposer;
    }

    public async Task<CatalogPage> GetItemsAsync(int pageIndex, int pageSize, int? brandId, int? typeId, CancellationToken cancellationToken = default)
    {
        var filterSpecification = new CatalogFilterSpecification(brandId, typeId);
        var filterPaginatedSpecification = new CatalogFilterPaginatedSpecification(pageSize * pageIndex, pageSize, brandId, typeId);

        var itemsOnPage = await _itemRepository.ListAsync(filterPaginatedSpecification, cancellationToken);
        var totalItems = await _itemRepository.CountAsync(filterSpecification, cancellationToken);

        return new CatalogPage(itemsOnPage.Select(ToInfo).ToList(), totalItems);
    }

    public async Task<IReadOnlyList<CatalogItemInfo>> GetItemsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        var items = await _itemRepository.ListAsync(new CatalogItemsSpecification(ids.ToArray()), cancellationToken);
        return items.Select(ToInfo).ToList();
    }

    public async Task<CatalogItemInfo?> GetItemAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : ToInfo(item);
    }

    public async Task<IReadOnlyList<CatalogLookupItem>> GetBrandsAsync(CancellationToken cancellationToken = default)
    {
        var brands = await _brandRepository.ListAsync(cancellationToken);
        return brands.Select(b => new CatalogLookupItem(b.Id, b.Brand)).ToList();
    }

    public async Task<IReadOnlyList<CatalogLookupItem>> GetTypesAsync(CancellationToken cancellationToken = default)
    {
        var types = await _typeRepository.ListAsync(cancellationToken);
        return types.Select(t => new CatalogLookupItem(t.Id, t.Type)).ToList();
    }

    public async Task UpdateItemDetailsAsync(int id, string name, decimal price, CancellationToken cancellationToken = default)
    {
        var existingCatalogItem = await _itemRepository.GetByIdAsync(id, cancellationToken);

        Guard.Against.Null(existingCatalogItem, nameof(existingCatalogItem));

        CatalogItem.CatalogItemDetails details = new(name, existingCatalogItem.Description, price);
        existingCatalogItem.UpdateDetails(details);
        await _itemRepository.UpdateAsync(existingCatalogItem, cancellationToken);
    }

    private CatalogItemInfo ToInfo(CatalogItem item) =>
        new(item.Id, item.Name, item.Description, item.Price, _uriComposer.ComposePicUri(item.PictureUri),
            item.CatalogBrandId, item.CatalogTypeId);
}

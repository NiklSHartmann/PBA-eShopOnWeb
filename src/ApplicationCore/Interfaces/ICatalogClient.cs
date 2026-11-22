using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

/// <summary>
/// Branch by Abstraction: alt, hvad monolitten læser og skriver i kataloget, går gennem dette interface.
/// LocalCatalogClient er den gamle kode (monolittens egen database), CatalogApiClient kalder Catalog.API.
/// Feature flaget UseCatalogService vælger, hvilken der bruges (se Web/Configuration/ConfigureWebServices.cs).
/// Billedstierne er færdige (pladsholderen er erstattet), uanset hvilken implementering der svarer.
/// </summary>
public interface ICatalogClient
{
    Task<CatalogPage> GetItemsAsync(int pageIndex, int pageSize, int? brandId, int? typeId, CancellationToken cancellationToken = default);

    /// <summary>Kun de varer, der findes; et id, der ikke findes, er bare ikke med i svaret.</summary>
    Task<IReadOnlyList<CatalogItemInfo>> GetItemsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    Task<CatalogItemInfo?> GetItemAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogLookupItem>> GetBrandsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogLookupItem>> GetTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>Den gamle admin-side (Pages/Admin/EditCatalogItem) retter kun navn og pris.</summary>
    Task UpdateItemDetailsAsync(int id, string name, decimal price, CancellationToken cancellationToken = default);
}

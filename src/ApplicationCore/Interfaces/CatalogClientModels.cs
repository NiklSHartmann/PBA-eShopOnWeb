using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

// Det, ICatalogClient svarer med. Ikke monolittens EF-klasser: så kan begge implementeringer levere dem,
// og koden, der bruger dem, ved ikke, om kataloget ligger i monolitten eller i Catalog.API.

public record CatalogItemInfo(int Id, string Name, string Description, decimal Price, string PictureUri,
    int CatalogBrandId, int CatalogTypeId);

public record CatalogLookupItem(int Id, string Name);

public record CatalogPage(IReadOnlyList<CatalogItemInfo> Items, int TotalItems);

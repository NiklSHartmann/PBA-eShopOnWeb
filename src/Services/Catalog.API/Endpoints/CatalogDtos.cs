namespace Catalog.API.Endpoints;

// Samme JSON som PublicApi's katalog-endpoints, så BlazorAdmin virker uændret, uanset om gatewayen
// sender kaldet hertil eller til PublicApi. Felter, der kun findes her (TotalItems og filteret ids),
// er tilføjet; de gamle klienter ignorerer dem.

public record CatalogItemDto(int Id, string Name, string Description, decimal Price, string PictureUri,
    int CatalogTypeId, int CatalogBrandId);

public record ListPagedCatalogItemResponse(List<CatalogItemDto> CatalogItems, int PageCount, int TotalItems);

public record GetByIdCatalogItemResponse(CatalogItemDto CatalogItem);

public record CreateCatalogItemResponse(CatalogItemDto CatalogItem);

public record UpdateCatalogItemResponse(CatalogItemDto CatalogItem);

public record DeleteCatalogItemResponse(string Status);

public record CatalogLookupDto(int Id, string Name);

public record ListCatalogBrandsResponse(List<CatalogLookupDto> CatalogBrands);

public record ListCatalogTypesResponse(List<CatalogLookupDto> CatalogTypes);

/// <summary>Samme form som monolittens ErrorDetails, som BlazorAdmin viser i en toast.</summary>
public record ErrorResponse(int StatusCode, string Message);

// Ekstra felter, som BlazorAdmin sender med (PictureBase64, CatalogBrand osv.), ignoreres.
public class CreateCatalogItemRequest
{
    public int CatalogBrandId { get; set; }
    public int CatalogTypeId { get; set; }
    public string? Description { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; set; }
}

public class UpdateCatalogItemRequest
{
    public int Id { get; set; }
    public int CatalogBrandId { get; set; }
    public int CatalogTypeId { get; set; }
    public string? Description { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; set; }
}

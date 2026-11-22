using Catalog.API.Domain;

namespace Catalog.API.Data;

/// <summary>Kataloget, som det ser ud i monolittens database lige nu.</summary>
public record MonolithCatalogSnapshot(
    IReadOnlyList<CatalogBrand> Brands,
    IReadOnlyList<CatalogType> Types,
    IReadOnlyList<CatalogItem> Items);

/// <summary>
/// Hvor kopien af kataloget læses fra. I drift er det monolittens SQL Server-database
/// (SqlMonolithCatalogSource); i testene en liste i hukommelsen.
/// </summary>
public interface IMonolithCatalogSource
{
    Task<MonolithCatalogSnapshot> ReadAsync(CancellationToken cancellationToken);
}

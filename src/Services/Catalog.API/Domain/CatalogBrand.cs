namespace Catalog.API.Domain;

/// <summary>Et mærke, fx ".NET" eller "Azure".</summary>
public class CatalogBrand
{
    // Til EF Core.
    private CatalogBrand()
    {
        Brand = string.Empty;
    }

    public CatalogBrand(string brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            throw new ArgumentException("Brand is required", nameof(brand));
        }

        Brand = brand;
    }

    public int Id { get; private set; }
    public string Brand { get; private set; }

    /// <summary>Kun til kopieringen fra monolitten: mærket beholder sit id.</summary>
    public static CatalogBrand CopyFromMonolith(int id, string brand) => new() { Id = id, Brand = brand };
}

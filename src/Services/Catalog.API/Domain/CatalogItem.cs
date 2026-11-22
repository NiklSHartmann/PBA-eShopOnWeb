namespace Catalog.API.Domain;

/// <summary>
/// En vare i kataloget. Samme felter som monolittens CatalogItem, men nu ejer Catalog.API dem.
/// Reglerne for navn, beskrivelse og pris ligger her, så de gælder både ved oprettelse og rettelse.
/// </summary>
public class CatalogItem
{
    // Til EF Core.
    private CatalogItem()
    {
        Name = string.Empty;
        Description = string.Empty;
        PictureUri = string.Empty;
    }

    public CatalogItem(int catalogTypeId, int catalogBrandId, string description, string name, decimal price, string pictureUri)
        : this()
    {
        UpdateDetails(name, description, price);
        UpdateBrand(catalogBrandId);
        UpdateType(catalogTypeId);
        PictureUri = pictureUri;
    }

    public int Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }

    /// <summary>Gemmes med pladsholderen http://catalogbaseurltobereplaced, ligesom i monolitten.</summary>
    public string PictureUri { get; private set; }

    public int CatalogTypeId { get; private set; }
    public int CatalogBrandId { get; private set; }

    public void UpdateDetails(string name, string description, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required", nameof(description));
        }

        if (price <= 0)
        {
            throw new ArgumentException("Price must be greater than zero", nameof(price));
        }

        Name = name;
        Description = description;
        Price = price;
    }

    public void UpdateBrand(int catalogBrandId)
    {
        if (catalogBrandId <= 0)
        {
            throw new ArgumentException("A brand is required", nameof(catalogBrandId));
        }

        CatalogBrandId = catalogBrandId;
    }

    public void UpdateType(int catalogTypeId)
    {
        if (catalogTypeId <= 0)
        {
            throw new ArgumentException("A type is required", nameof(catalogTypeId));
        }

        CatalogTypeId = catalogTypeId;
    }

    /// <summary>
    /// Kun til kopieringen fra monolitten. Varen beholder sit id, så kurve, ordrer og lageret
    /// stadig peger på den rigtige vare. Data tages, som de er; reglerne gjaldt allerede i monolitten.
    /// </summary>
    public static CatalogItem CopyFromMonolith(int id, int catalogTypeId, int catalogBrandId,
        string description, string name, decimal price, string pictureUri)
    {
        return new CatalogItem
        {
            Id = id,
            CatalogTypeId = catalogTypeId,
            CatalogBrandId = catalogBrandId,
            Description = description,
            Name = name,
            Price = price,
            PictureUri = pictureUri
        };
    }
}

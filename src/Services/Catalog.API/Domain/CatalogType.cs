namespace Catalog.API.Domain;

/// <summary>En varetype, fx "Mug" eller "T-Shirt".</summary>
public class CatalogType
{
    // Til EF Core.
    private CatalogType()
    {
        Type = string.Empty;
    }

    public CatalogType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Type is required", nameof(type));
        }

        Type = type;
    }

    public int Id { get; private set; }
    public string Type { get; private set; }

    /// <summary>Kun til kopieringen fra monolitten: typen beholder sit id.</summary>
    public static CatalogType CopyFromMonolith(int id, string type) => new() { Id = id, Type = type };
}

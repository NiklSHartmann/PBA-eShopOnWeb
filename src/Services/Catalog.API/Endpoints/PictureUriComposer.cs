namespace Catalog.API.Endpoints;

/// <summary>
/// Billedstier gemmes med pladsholderen http://catalogbaseurltobereplaced, ligesom i monolitten.
/// Før de sendes ud, erstattes den med CatalogBaseUrl. Er den tom, bliver stien fx /images/products/1.png,
/// som Web leverer, og som browseren henter gennem gatewayen.
/// </summary>
public class PictureUriComposer
{
    public const string Placeholder = "http://catalogbaseurltobereplaced";

    private readonly string _catalogBaseUrl;

    public PictureUriComposer(string catalogBaseUrl)
    {
        _catalogBaseUrl = catalogBaseUrl;
    }

    public string Compose(string pictureUri) => pictureUri.Replace(Placeholder, _catalogBaseUrl);
}

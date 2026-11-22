using System.Net;
using System.Net.Http.Headers;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Web.Services;

/// <summary>
/// Den nye vej: kataloget hentes fra Catalog.API over HTTP. Bruges, når feature flaget UseCatalogService er true.
/// Web kalder Catalog.API direkte inde i Docker-netværket (catalogapi:8080, et navn som begge instanser svarer på),
/// ikke gennem gatewayen, så butikkens egne kald ikke tæller med i gatewayens rate limit.
/// Fejler kaldet, logges det og fejlen kastes videre: hellere en fejlside end et katalog fra den gamle database.
/// </summary>
public class CatalogApiClient : ICatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenClaimsService _tokenClaimsService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CatalogApiClient> _logger;

    // HttpClient bliver givet af ASP.NET Core med BaseAddress og timeout sat (se ConfigureWebServices).
    public CatalogApiClient(HttpClient httpClient,
        ITokenClaimsService tokenClaimsService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CatalogApiClient> logger)
    {
        _httpClient = httpClient;
        _tokenClaimsService = tokenClaimsService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<CatalogPage> GetItemsAsync(int pageIndex, int pageSize, int? brandId, int? typeId, CancellationToken cancellationToken = default)
    {
        var url = $"api/catalog-items?pageIndex={pageIndex}&pageSize={pageSize}";
        if (brandId.HasValue)
        {
            url += $"&catalogBrandId={brandId.Value}";
        }

        if (typeId.HasValue)
        {
            url += $"&catalogTypeId={typeId.Value}";
        }

        var response = await GetAsync<ItemListResponse>(url, cancellationToken);
        return new CatalogPage(response.CatalogItems.Select(ToInfo).ToList(), response.TotalItems);
    }

    public async Task<IReadOnlyList<CatalogItemInfo>> GetItemsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<CatalogItemInfo>();
        }

        // pageSize udeladt = alle varer, der matcher filteret.
        var url = "api/catalog-items?" + string.Join("&", ids.Select(id => $"ids={id}"));
        var response = await GetAsync<ItemListResponse>(url, cancellationToken);
        return response.CatalogItems.Select(ToInfo).ToList();
    }

    public async Task<CatalogItemInfo?> GetItemAsync(int id, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/catalog-items/{id}");
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, request);
        var body = await ReadAsync<ItemResponse>(response, request, cancellationToken);
        return ToInfo(body.CatalogItem);
    }

    public async Task<IReadOnlyList<CatalogLookupItem>> GetBrandsAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<BrandListResponse>("api/catalog-brands", cancellationToken);
        return response.CatalogBrands.Select(b => new CatalogLookupItem(b.Id, b.Name)).ToList();
    }

    public async Task<IReadOnlyList<CatalogLookupItem>> GetTypesAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetAsync<TypeListResponse>("api/catalog-types", cancellationToken);
        return response.CatalogTypes.Select(t => new CatalogLookupItem(t.Id, t.Name)).ToList();
    }

    public async Task UpdateItemDetailsAsync(int id, string name, decimal price, CancellationToken cancellationToken = default)
    {
        // Catalog.API's PUT tager hele varen, så resten hentes først.
        var current = await GetItemAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Catalog item {id} does not exist in Catalog.API");

        using var request = new HttpRequestMessage(HttpMethod.Put, "api/catalog-items")
        {
            Content = JsonContent.Create(new UpdateRequest(current.Id, current.CatalogBrandId, current.CatalogTypeId,
                current.Description, name, price))
        };

        // Catalog.API kræver rollen Administrators. Web laver et JWT til den indloggede bruger, som BlazorAdmin også får det.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenForCurrentUserAsync());

        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response, request);
        _logger.LogInformation("Catalog item {CatalogItemId} updated in Catalog.API", id);
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response, request);
        return await ReadAsync<T>(response, request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Could not reach Catalog.API for {Method} {Uri}", request.Method, request.RequestUri);
            throw;
        }
    }

    private void EnsureSuccess(HttpResponseMessage response, HttpRequestMessage request)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        _logger.LogError("Catalog.API answered {StatusCode} to {Method} {Uri}", (int)response.StatusCode, request.Method, request.RequestUri);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Catalog.API returned an empty body for {request.Method} {request.RequestUri}");
    }

    private async Task<string> GetTokenForCurrentUserAsync()
    {
        var userName = _httpContextAccessor.HttpContext?.User.Identity?.Name
            ?? throw new InvalidOperationException("Only a logged-in administrator can change the catalog");
        return await _tokenClaimsService.GetTokenAsync(userName);
    }

    private static CatalogItemInfo ToInfo(ItemDto item) =>
        new(item.Id, item.Name, item.Description, item.Price, item.PictureUri, item.CatalogBrandId, item.CatalogTypeId);

    // Catalog.API's JSON (samme form som PublicApi's, plus TotalItems). Kun de felter, Web bruger.
    private sealed record ItemDto(int Id, string Name, string Description, decimal Price, string PictureUri,
        int CatalogTypeId, int CatalogBrandId);

    private sealed record ItemListResponse(List<ItemDto> CatalogItems, int TotalItems);

    private sealed record ItemResponse(ItemDto CatalogItem);

    private sealed record LookupDto(int Id, string Name);

    private sealed record BrandListResponse(List<LookupDto> CatalogBrands);

    private sealed record TypeListResponse(List<LookupDto> CatalogTypes);

    private sealed record UpdateRequest(int Id, int CatalogBrandId, int CatalogTypeId, string Description, string Name, decimal Price);
}

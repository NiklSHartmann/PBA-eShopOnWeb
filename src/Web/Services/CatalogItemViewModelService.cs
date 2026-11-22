using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.ViewModels;

namespace Microsoft.eShopWeb.Web.Services;

public class CatalogItemViewModelService : ICatalogItemViewModelService
{
    private readonly ICatalogClient _catalogClient;

    public CatalogItemViewModelService(ICatalogClient catalogClient)
    {
        _catalogClient = catalogClient;
    }

    // Skriver gennem ICatalogClient: når kataloget er flyttet, må monolittens egen kopi ikke længere ændres.
    public Task UpdateCatalogItem(CatalogItemViewModel viewModel)
    {
        return _catalogClient.UpdateItemDetailsAsync(viewModel.Id, viewModel.Name ?? string.Empty, viewModel.Price);
    }
}

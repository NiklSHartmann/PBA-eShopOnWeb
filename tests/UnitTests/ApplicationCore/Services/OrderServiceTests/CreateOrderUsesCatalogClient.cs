using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.UnitTests.Builders;
using NSubstitute;
using Xunit;

namespace Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.OrderServiceTests;

/// <summary>
/// Branch by Abstraction: OrderService ved ikke, om kataloget ligger i monolitten eller i Catalog.API.
/// Navn og billede i ordren skal komme fra ICatalogClient, prisen fra kurven.
/// </summary>
public class CreateOrderUsesCatalogClient
{
    [Fact]
    public async Task Order_lines_get_name_and_picture_from_the_catalog_client()
    {
        var catalog = Substitute.For<ICatalogClient>();
        IReadOnlyList<CatalogItemInfo> catalogItems = new List<CatalogItemInfo>
        {
            new(5, "Roslyn Red Sheet", "Sheet", 8.5m, "/images/products/5.png", CatalogBrandId: 2, CatalogTypeId: 3)
        };
        catalog.GetItemsByIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(catalogItems);
        var orders = Substitute.For<IRepository<Order>>();
        var service = new OrderService(catalog, orders);

        var order = await service.CreateOrderAsync("buyer", new[] { new OrderItemRequest(5, 7.25m, 2) }, new AddressBuilder().Build());

        var line = Assert.Single(order.OrderItems);
        Assert.Equal("Roslyn Red Sheet", line.ItemOrdered.ProductName);
        Assert.Equal("/images/products/5.png", line.ItemOrdered.PictureUri);
        Assert.Equal(7.25m, line.UnitPrice);
        await orders.Received(1).AddAsync(order, Arg.Any<CancellationToken>());
    }
}

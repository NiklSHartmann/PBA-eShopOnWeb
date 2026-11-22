using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly ICatalogClient _catalogClient;

    // IRepository<Basket> er fjernet: monolitten har ikke længere adgang til kurven.
    // Navn og billede til ordren hentes gennem ICatalogClient, så de kommer fra Catalog.API, når flaget er slået til.
    public OrderService(ICatalogClient catalogClient,
        IRepository<Order> orderRepository)
    {
        _orderRepository = orderRepository;
        _catalogClient = catalogClient;
    }

    public async Task<Order> CreateOrderAsync(string buyerId, IReadOnlyCollection<OrderItemRequest> items, Address shippingAddress)
    {
        Guard.Against.EmptyBasketOnCheckout(items);
        Guard.Against.NullOrEmpty(buyerId, nameof(buyerId));

        var catalogItems = await _catalogClient.GetItemsByIdsAsync(items.Select(item => item.CatalogItemId).Distinct().ToList());

        var orderItems = items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, catalogItem.PictureUri);
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(buyerId, shippingAddress, orderItems);

        await _orderRepository.AddAsync(order);

        return order;
    }
}

using Catalog.API.Domain;
using Xunit;

namespace Catalog.API.UnitTests.Domain;

public class CatalogItemTests
{
    [Fact]
    public void A_new_item_keeps_its_details()
    {
        var item = new CatalogItem(catalogTypeId: 2, catalogBrandId: 1, "A mug", "Mug", 8.5m, "pic.png");

        Assert.Equal("Mug", item.Name);
        Assert.Equal("A mug", item.Description);
        Assert.Equal(8.5m, item.Price);
        Assert.Equal(1, item.CatalogBrandId);
        Assert.Equal(2, item.CatalogTypeId);
    }

    [Theory]
    [InlineData("", "A mug", 8.5)]
    [InlineData("Mug", " ", 8.5)]
    [InlineData("Mug", "A mug", 0)]
    [InlineData("Mug", "A mug", -1)]
    public void Invalid_details_are_rejected(string name, string description, decimal price)
    {
        var item = new CatalogItem(1, 1, "A mug", "Mug", 8.5m, "pic.png");

        Assert.Throws<ArgumentException>(() => item.UpdateDetails(name, description, price));
        Assert.Equal("Mug", item.Name);
    }

    [Fact]
    public void A_brand_or_type_is_required()
    {
        Assert.Throws<ArgumentException>(() => new CatalogItem(catalogTypeId: 1, catalogBrandId: 0, "A mug", "Mug", 8.5m, "pic.png"));
        Assert.Throws<ArgumentException>(() => new CatalogItem(catalogTypeId: 0, catalogBrandId: 1, "A mug", "Mug", 8.5m, "pic.png"));
    }

    [Fact]
    public void A_copy_from_the_monolith_keeps_its_id()
    {
        var item = CatalogItem.CopyFromMonolith(12, 1, 2, "Desc", "Name", 10m, "pic.png");

        Assert.Equal(12, item.Id);
    }
}

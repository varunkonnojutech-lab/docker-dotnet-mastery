using DockerDemo.Api;

namespace DockerDemo.Tests;

/// <summary>Store rules, tested without HTTP: the validation the POST
/// endpoint relies on lives here, so it is provable in isolation.</summary>
public class StoreTests
{
    [Fact]
    public void SeededCatalogHasThreeProducts()
    {
        Assert.Equal(3, new ProductsStore().All().Count);
    }

    [Fact]
    public void AddAssignsIncrementingIds()
    {
        var store = new ProductsStore();
        var first = store.Add("Mouse", 25.00m, true);
        var second = store.Add("Mousepad", 15.00m, true);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id + 1, second.Id);
    }

    [Theory]
    [InlineData("", 10.00)]
    [InlineData("   ", 10.00)]
    [InlineData("Valid name", -1.00)]
    public void AddRejectsInvalidInput(string name, decimal price)
    {
        Assert.Null(new ProductsStore().Add(name, price, true));
    }

    [Fact]
    public void RemoveReturnsFalseForUnknownId()
    {
        Assert.False(new ProductsStore().Remove(999));
    }

    [Fact]
    public void RemoveDeletesAndSecondRemoveFails()
    {
        var store = new ProductsStore();
        Assert.True(store.Remove(1));
        Assert.False(store.Remove(1));
        Assert.Null(store.Get(1));
    }
}

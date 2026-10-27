using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DockerDemo.Tests;

/// <summary>One end-to-end smoke test per route, including the /health
/// endpoint the Dockerfiles and Compose file rely on.</summary>
public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetProductsReturnsSeededCatalog()
    {
        // Assert on the seeded items, not an exact count: this class shares
        // one app (and one store) across its tests, so a POST test may
        // have added products before this test runs.
        var products = await _client.GetFromJsonAsync<List<DockerDemo.Api.Product>>("/products");
        Assert.NotNull(products);
        Assert.True(products.Count >= 3);
        Assert.Contains(products, p => p.Name == "Mechanical Keyboard");
        Assert.Contains(products, p => p.Name == "USB-C Dock");
        Assert.Contains(products, p => p.Name == "4K Monitor");
    }

    [Fact]
    public async Task GetUnknownProductReturns404()
    {
        var response = await _client.GetAsync("/products/999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostThenGetRoundTrips()
    {
        var created = await _client.PostAsJsonAsync("/products",
            new { Name = "Webcam", Price = 59.99m, InStock = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var product = await created.Content.ReadFromJsonAsync<DockerDemo.Api.Product>();
        Assert.NotNull(product);
        Assert.Equal("Webcam", product.Name);
        Assert.Equal(59.99m, product.Price);

        var fetched = await _client.GetAsync($"/products/{product.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task PostInvalidProductReturns400()
    {
        var response = await _client.PostAsJsonAsync("/products",
            new { Name = "", Price = 10.00m, InStock = true });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

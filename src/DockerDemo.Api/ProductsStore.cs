namespace DockerDemo.Api;

/// <summary>A product in the demo catalog. Kept deliberately small: this repo
/// is about the Dockerfiles, not the domain model.</summary>
public record Product(int Id, string Name, decimal Price, bool InStock);

/// <summary>Thread-safe in-memory catalog. A singleton in Program.cs, so the
/// API and the tests exercise the same behaviour.</summary>
public sealed class ProductsStore
{
    private readonly List<Product> _products = new()
    {
        new Product(1, "Mechanical Keyboard", 89.99m, true),
        new Product(2, "USB-C Dock", 129.00m, true),
        new Product(3, "4K Monitor", 349.00m, false),
    };

    private readonly object _gate = new();
    private int _nextId = 4;

    public IReadOnlyList<Product> All()
    {
        lock (_gate)
        {
            return _products.ToList();
        }
    }

    public Product? Get(int id)
    {
        lock (_gate)
        {
            return _products.FirstOrDefault(p => p.Id == id);
        }
    }

    /// <summary>Adds a product. Returns null when the input is invalid
    /// (blank name or negative price) so the endpoint can answer 400.</summary>
    public Product? Add(string name, decimal price, bool inStock)
    {
        if (string.IsNullOrWhiteSpace(name) || price < 0)
        {
            return null;
        }

        lock (_gate)
        {
            var product = new Product(_nextId++, name.Trim(), price, inStock);
            _products.Add(product);
            return product;
        }
    }

    public bool Remove(int id)
    {
        lock (_gate)
        {
            var product = _products.FirstOrDefault(p => p.Id == id);
            return product is not null && _products.Remove(product);
        }
    }
}

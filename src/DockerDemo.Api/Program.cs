using DockerDemo.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ProductsStore>();
var app = builder.Build();

// Health endpoint: the one Docker and orchestrators check. Keep it cheap
// (no database, no downstream call) so a slow dependency never makes a
// healthy container look dead.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/products", (ProductsStore store) => Results.Ok(store.All()));

app.MapGet("/products/{id:int}", (int id, ProductsStore store) =>
    store.Get(id) is { } product ? Results.Ok(product) : Results.NotFound());

app.MapPost("/products", (ProductInput input, ProductsStore store) =>
{
    var created = store.Add(input.Name, input.Price, input.InStock);
    return created is null
        ? Results.BadRequest(new { error = "Name must not be blank and price must not be negative." })
        : Results.Created($"/products/{created.Id}", created);
});

app.MapDelete("/products/{id:int}", (int id, ProductsStore store) =>
    store.Remove(id) ? Results.NoContent() : Results.NotFound());

app.Run();

public record ProductInput(string Name, decimal Price, bool InStock);

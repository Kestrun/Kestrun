using Serilog;
using Kestrun.Hosting;

// Configure logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

// Create server
var server = new KestrunHost("OpenAPI XML Example", Directory.GetCurrentDirectory());

// Add OpenAPI configuration
server.Options.OpenApiOptions.Title = "Product API";
server.Options.OpenApiOptions.Version = "1.0";
server.Options.OpenApiOptions.Description = "Example demonstrating OpenAPI 3.2 XML modeling with attributes, namespaces, and wrapped arrays";

// Define a simple product endpoint that returns XML
server.AddMapRoute("/products/{id}", HttpVerb.Get, async context =>
{
    var id = context.Request.RouteValues["id"]?.ToString() ?? "0";
    var product = new Product
    {
        Id = int.Parse(id),
        Name = "Sample Product",
        Price = 19.99m,
        Items = new[] { "Item1", "Item2", "Item3" }
    };
    
    await context.Response.WriteXmlResponseAsync(product, 200);
});

// Configure endpoint
server.AddEndpoint(port: 5000);

// Start server
await server.StartAsync();

Console.WriteLine("Server started. Navigate to http://localhost:5000/swagger to view OpenAPI documentation");
Console.WriteLine("Try: http://localhost:5000/products/123");
Console.WriteLine("Press any key to stop...");
Console.ReadKey();

await server.StopAsync();

/// <summary>
/// Product model demonstrating OpenAPI XML attributes.
/// </summary>
[OpenApiSchemaComponent(Title = "Product", Description = "A product with XML-specific serialization")]
public class Product
{
    /// <summary>
    /// Product ID - rendered as XML attribute
    /// </summary>
    [OpenApiProperty(Description = "Unique product identifier")]
    [OpenApiXml(Name = "id", Attribute = true)]
    public int Id { get; set; }

    /// <summary>
    /// Product name
    /// </summary>
    [OpenApiProperty(Description = "Product name")]
    [OpenApiXml(Name = "ProductName")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Product price with custom namespace
    /// </summary>
    [OpenApiProperty(Description = "Product price in USD")]
    [OpenApiXml(Name = "Price", Namespace = "http://example.com/pricing", Prefix = "price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Array of items with wrapped XML
    /// </summary>
    [OpenApiProperty(Description = "List of product items")]
    [OpenApiXml(Name = "Item", Wrapped = true)]
    public string[] Items { get; set; } = Array.Empty<string>();
}

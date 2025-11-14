using Kestrun.Razor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrunTests.Razor;

public class PwshKestrunModelTests
{
    private static PwshKestrunModel CreateModel(DefaultHttpContext ctx)
    {
        var model = new PwshKestrunModel
        {
            PageContext = new PageContext { HttpContext = ctx }
        };
        return model;
    }

    [Fact]
    [Trait("Category", "Razor")]
    public void Data_Returns_Item_From_HttpContext()
    {
        var ctx = new DefaultHttpContext();
        var payload = new { Hello = "World" };
        ctx.Items["PageModel"] = payload;

        var model = CreateModel(ctx);
        Assert.Same(payload, model.Data);
    }

    [Fact]
    [Trait("Category", "Razor")]
    public void Query_Returns_Value_And_Null_When_Missing()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["foo"] = "bar"
        });

        var model = CreateModel(ctx);
        Assert.Equal("bar", model.Query("foo"));
        Assert.Null(model.Query("missing"));
    }

    [Fact]
    [Trait("Category", "Razor")]
    public void Config_Is_Resolved_From_RequestServices()
    {
        var mem = new Dictionary<string, string?> { ["App:Name"] = "Kestrun" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(mem).Build();
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(config);
        var sp = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext { RequestServices = sp };
        var model = CreateModel(ctx);

        Assert.Equal("Kestrun", model.Config["App:Name"]);
    }
}

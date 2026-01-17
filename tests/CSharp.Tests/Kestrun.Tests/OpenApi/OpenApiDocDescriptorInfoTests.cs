using System.Collections;
using System.Text.Json;
using Kestrun.Hosting;
using Kestrun.OpenApi;
using Microsoft.OpenApi;
using Serilog;
using Xunit;

namespace KestrunTests.OpenApi;

public sealed class OpenApiDocDescriptorInfoTests
{
    [Theory]
    [InlineData(OpenApiSpecVersion.OpenApi3_0)]
    [InlineData(OpenApiSpecVersion.OpenApi3_1)]
    [InlineData(OpenApiSpecVersion.OpenApi3_2)]
    public void CreateInfoContact_NormalizesExtensions_AndSerializes(OpenApiSpecVersion version)
    {
        using var host = new KestrunHost("Tests", Log.Logger);
        var d = new OpenApiDocDescriptor(host, OpenApiDocDescriptor.DefaultDocumentationId);

        var extensions = new Hashtable
        {
            ["contact-department"] = "Developer Relations",
            ["x-contact-hours"] = "9am-5pm PST",
            ["x-logo"] = new Hashtable
            {
                ["url"] = "https://redocly.github.io/redoc/museum-logo.png",
                ["altText"] = "Museum logo",
            },
            ["nullValue"] = null,
            [" "] = "ignored",
        };

        var contact = OpenApiDocDescriptor.CreateInfoContact(
            name: "API Support",
            url: new Uri("https://example.com/support", UriKind.Absolute),
            email: "support@example.com",
            extensions: extensions);

        d.Document.Info = new OpenApiInfo
        {
            Title = "Document Info API",
            Version = "1.0.0",
            Contact = contact,
        };

        using var jsonDoc = JsonDocument.Parse(d.ToJson(version));
        var info = jsonDoc.RootElement.GetProperty("info");
        var contactJson = info.GetProperty("contact");

        Assert.Equal("API Support", contactJson.GetProperty("name").GetString());
        Assert.Equal("support@example.com", contactJson.GetProperty("email").GetString());
        Assert.Equal("https://example.com/support", contactJson.GetProperty("url").GetString());

        Assert.Equal("Developer Relations", contactJson.GetProperty("x-contact-department").GetString());
        Assert.Equal("9am-5pm PST", contactJson.GetProperty("x-contact-hours").GetString());

        var logo = contactJson.GetProperty("x-logo");
        Assert.Equal("https://redocly.github.io/redoc/museum-logo.png", logo.GetProperty("url").GetString());
        Assert.Equal("Museum logo", logo.GetProperty("altText").GetString());

        Assert.False(contactJson.TryGetProperty("x-nullValue", out _));
    }
}

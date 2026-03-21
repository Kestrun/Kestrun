#if NET10_0_OR_GREATER
using System.Management.Automation;
using System.Reflection;
using Kestrun.Forms;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Utilities;
using Microsoft.OpenApi;
using Xunit;

namespace KestrunTests.Hosting;

[Collection("SharedStateSerial")]
public class KestrunHostFormRouteExtensionsTests
{
    private static readonly Type ExtensionsType = typeof(KestrunHostMapExtensions);

    [Fact]
    [Trait("Category", "Hosting")]
    public void BuildFormRouteMapOptions_WithAllowAnonymousAndAuth_ThrowsArgumentException()
    {
        var options = CreateFormOptions();
        var script = ScriptBlock.Create("Write-Output 'ok'");

        var ex = Assert.Throws<TargetInvocationException>(() =>
            _ = InvokePrivate(
                "BuildFormRouteMapOptions",
                "/upload",
                script,
                options,
                new[] { "Bearer" },
                null,
                null,
                true,
                true,
                null,
                null,
                null,
                null,
                null));

        _ = Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void BuildFormRouteMapOptions_WithOpenApiEnabled_PopulatesRouteAndMetadata()
    {
        var options = CreateFormOptions();
        var script = ScriptBlock.Create("Write-Output 'ok'");

        var routeOptions = Assert.IsType<MapRouteOptions>(InvokePrivate(
            "BuildFormRouteMapOptions",
            "/upload",
            script,
            options,
            new[] { "Bearer" },
            new[] { "AdminOnly" },
            "forms-cors",
            false,
            false,
            "UploadForm",
            new[] { "forms" },
            "Upload a multipart payload.",
            "Uploads files and form fields.",
            new[] { "forms-doc" }));

        Assert.Equal("/upload", routeOptions.Pattern);
        Assert.Equal([HttpVerb.Post], routeOptions.HttpVerbs);
        Assert.Equal("forms-cors", routeOptions.CorsPolicy);
        Assert.Contains("Bearer", routeOptions.RequireSchemes);
        Assert.Contains("AdminOnly", routeOptions.RequirePolicies);

        var metadata = Assert.IsType<OpenAPIPathMetadata>(routeOptions.OpenAPI[HttpVerb.Post]);
        Assert.Equal("UploadForm", metadata.OperationId);
        Assert.Equal("Upload a multipart payload.", metadata.Summary);
        Assert.Equal("Uploads files and form fields.", metadata.Description);
        Assert.Equal(["forms"], metadata.Tags);
        var documentIds = Assert.IsType<string[]>(metadata.DocumentId);
        Assert.Equal(["forms-doc"], documentIds);
        var requestBody = Assert.IsType<Microsoft.OpenApi.OpenApiRequestBody>(metadata.RequestBody);
        Assert.Contains("multipart/form-data", requestBody.Content.Keys);
        Assert.Contains("application/x-www-form-urlencoded", requestBody.Content.Keys);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void AddFormRoute_WithParentRouteGroup_MergesParentPattern()
    {
        using var host = new KestrunHost("FormRouteTests", AppContext.BaseDirectory);
        host.EnableConfiguration();
        host.RouteGroupStack.Push(new MapRouteOptions
        {
            Pattern = "/api",
        });

        var options = CreateFormOptions();
        _ = host.AddFormRoute("/upload", ScriptBlock.Create("Write-Output 'ok'"), options, null, null, null, allowAnonymous: true);

        var saved = host.GetMapRouteOptions("/api/upload", HttpVerb.Post);
        Assert.NotNull(saved);
        Assert.Equal([HttpVerb.Post], saved.HttpVerbs);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void BuildOpenApiRequestBody_BuildsMultipartAndUrlEncodedSchemas()
    {
        var options = new KrFormOptions();
        options.AllowedContentTypes.Clear();
        options.AllowedContentTypes.Add("multipart/form-data");
        options.AllowedContentTypes.Add("application/x-www-form-urlencoded");

        var fileRule = new KrFormPartRule
        {
            Name = "file",
            Required = true,
            AllowMultiple = false,
            StoreToDisk = true,
        };
        fileRule.AllowedContentTypes.Add("image/png");

        var tagsRule = new KrFormPartRule
        {
            Name = "tags",
            Required = false,
            AllowMultiple = true,
            StoreToDisk = false,
        };
        tagsRule.AllowedContentTypes.Add("text/plain");

        options.Rules.Add(fileRule);
        options.Rules.Add(tagsRule);

        var requestBody = Assert.IsType<Microsoft.OpenApi.OpenApiRequestBody>(InvokePrivate("BuildOpenApiRequestBody", options));
        Assert.True(requestBody.Required);

        var multipart = Assert.IsType<Microsoft.OpenApi.OpenApiMediaType>(requestBody.Content["multipart/form-data"]);
        var urlEncoded = Assert.IsType<Microsoft.OpenApi.OpenApiMediaType>(requestBody.Content["application/x-www-form-urlencoded"]);

        var multipartObject = Assert.IsType<Microsoft.OpenApi.OpenApiSchema>(multipart.Schema);
        Assert.Equal(JsonSchemaType.Object, multipartObject.Type);
        Assert.Contains("file", multipartObject.Required!);
        var fileSchema = Assert.IsType<Microsoft.OpenApi.OpenApiSchema>(multipartObject.Properties["file"]);
        var tagsSchema = Assert.IsType<Microsoft.OpenApi.OpenApiSchema>(multipartObject.Properties["tags"]);
        Assert.Equal("binary", fileSchema.Format);
        Assert.Equal(JsonSchemaType.Array, tagsSchema.Type);
        Assert.Equal("string", tagsSchema.Items!.Type.ToString().ToLowerInvariant());

        var urlEncodedObject = Assert.IsType<Microsoft.OpenApi.OpenApiSchema>(urlEncoded.Schema);
        Assert.Equal(JsonSchemaType.Object, urlEncodedObject.Type);
        var urlEncodedFileSchema = Assert.IsType<Microsoft.OpenApi.OpenApiSchema>(urlEncodedObject.Properties["file"]);
        Assert.Null(urlEncodedFileSchema.Format);
        Assert.NotNull(multipart.Encoding);
        var multipartEncoding = Assert.IsType<Dictionary<string, Microsoft.OpenApi.OpenApiEncoding>>(multipart.Encoding);
        Assert.Equal("image/png", multipartEncoding["file"].ContentType);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void IsProbablyFileRule_DetectsFileAndNonFileCases()
    {
        var byStorage = new KrFormPartRule { Name = "upload", StoreToDisk = true };
        var byExtension = new KrFormPartRule { Name = "doc", StoreToDisk = false };
        byExtension.AllowedExtensions.Add(".pdf");

        var byBinaryType = new KrFormPartRule { Name = "raw", StoreToDisk = false };
        byBinaryType.AllowedContentTypes.Add("application/octet-stream");

        var textOnly = new KrFormPartRule { Name = "note", StoreToDisk = false };
        textOnly.AllowedContentTypes.Add("text/plain");
        textOnly.AllowedContentTypes.Add("application/json");
        textOnly.AllowedContentTypes.Add("application/x-www-form-urlencoded");

        Assert.True(Assert.IsType<bool>(InvokePrivate("IsProbablyFileRule", byStorage)));
        Assert.True(Assert.IsType<bool>(InvokePrivate("IsProbablyFileRule", byExtension)));
        Assert.True(Assert.IsType<bool>(InvokePrivate("IsProbablyFileRule", byBinaryType)));
        Assert.False(Assert.IsType<bool>(InvokePrivate("IsProbablyFileRule", textOnly)));
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void MergeHelpers_CombineAndOverrideExpectedValues()
    {
        var mergedPattern = Assert.IsType<string>(InvokePrivate("MergePattern", "/api", "upload"));
        Assert.Equal("/api/upload", mergedPattern);

        var mergedUnique = Assert.IsType<string[]>(InvokePrivate("MergeUnique", new[] { "A", "", "B" }, new[] { "B", "C" }));
        Assert.Equal(3, mergedUnique.Length);
        Assert.Contains("A", mergedUnique);
        Assert.Contains("B", mergedUnique);
        Assert.Contains("C", mergedUnique);

        var mergedArguments = Assert.IsType<Dictionary<string, object?>>(InvokePrivate(
            "MergeArguments",
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["Port"] = 5000, ["Name"] = "base" },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["Name"] = "override", ["Enabled"] = true }));
        Assert.Equal(5000, Assert.IsType<int>(mergedArguments["Port"]));
        Assert.Equal("override", Assert.IsType<string>(mergedArguments["Name"]));
        Assert.True(Assert.IsType<bool>(mergedArguments["Enabled"]));
    }

    private static KrFormOptions CreateFormOptions()
    {
        var options = new KrFormOptions();
        options.AllowedContentTypes.Clear();
        options.AllowedContentTypes.Add("multipart/form-data");
        options.AllowedContentTypes.Add("application/x-www-form-urlencoded");
        options.Rules.Add(new KrFormPartRule
        {
            Name = "file",
            Required = true,
            AllowMultiple = false,
            StoreToDisk = true,
        });
        options.Rules.Add(new KrFormPartRule
        {
            Name = "name",
            Required = false,
            AllowMultiple = false,
            StoreToDisk = false,
        });
        return options;
    }

    private static object? InvokePrivate(string methodName, params object?[] args)
    {
        var method = ExtensionsType
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, methodName, StringComparison.Ordinal)
                && candidate.GetParameters().Length == args.Length);

        Assert.NotNull(method);
        return method.Invoke(null, args);
    }
}
#endif

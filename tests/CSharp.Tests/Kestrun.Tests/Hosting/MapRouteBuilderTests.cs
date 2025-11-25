using System.Management.Automation;
using Kestrun.Hosting;
using Kestrun.Hosting.Options;
using Kestrun.OpenApi;
using Kestrun.Authentication;
using Serilog;
using Xunit;
using Kestrun.Utilities;
using Kestrun.Scripting;

namespace KestrunTests.Hosting;

public class MapRouteBuilderTests
{
    static MapRouteBuilderTests() => Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().CreateLogger();

    // Component types for reference-based tests -------------------------------------------------
    [OpenApiResponseComponent]
    private class ResponseComponentHolder
    {
        [OpenApiResponse(Description = "ok", ContentType = "application/json")]
        public object Ok { get; set; } = new { Value = 1 };
    }

    [OpenApiParameterComponent(Description = "id param")]
    private class ParameterComponentHolder
    {
        [OpenApiParameter(Name = "id", In = "path", Description = "identifier", Required = true)]
        public int Id { get; set; } = 3;
    }

    [OpenApiRequestBodyComponent(Key = "ReqBody", Description = "req body", ContentType = "application/json", Required = true, Example = "ex-val")]
    private class RequestBodyComponentHolder
    {
        public string Name { get; set; } = "Alice";
    }

    private static KestrunHost NewHost() => new("TestApp", Log.Logger);

    [Fact]
    public void CreateAndToString_PopulatesVerbsAndPattern()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/users/{id}", [HttpVerb.Get, HttpVerb.Post]);
        // Current implementation uses enum names (Get,Post) not upper-case
        Assert.Equal("TestApp Get,Post /users/{id}", builder.ToString());
        Assert.Equal(2, builder.HttpVerbs.Count);
        Assert.Contains(HttpVerb.Get, builder.HttpVerbs);
        Assert.Contains(HttpVerb.Post, builder.HttpVerbs);
    }

    [Fact]
    public void AddScriptBlock_SetsPowerShellCode()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/ps", [HttpVerb.Get]);
        var sb = ScriptBlock.Create("Write-Output 'Hello'");
        _ = builder.AddScriptBlock(sb);
        Assert.Equal(ScriptLanguage.PowerShell, builder.ScriptCode.Language);
        Assert.Contains("Write-Output", builder.ScriptCode.Code);
    }

    [Fact]
    public void AddCodeBlock_CSharp_SetsLanguageAndImports()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/cs", [HttpVerb.Post]);
        _ = builder.AddCodeBlock("Context.Response.WriteJsonResponse(new{msg=\"hi\"});", ScriptLanguage.CSharp, extraImports: ["System", "System.Linq"]);
        Assert.Equal(ScriptLanguage.CSharp, builder.ScriptCode.Language);
        Assert.Equal("Context.Response.WriteJsonResponse(new{msg=\"hi\"});", builder.ScriptCode.Code);
        Assert.Contains("System", builder.ScriptCode.ExtraImports!);
    }

    [Fact]
    public void AddCodeFromFile_InfersLanguageAndLoadsContent()
    {
        var tmp = Path.GetTempFileName().Replace(".tmp", ".cs");
        File.WriteAllText(tmp, "// sample code\nreturn 1;");
        try
        {
            var host = NewHost();
            var builder = MapRouteBuilder.Create(host, "/file", [HttpVerb.Get]);
            _ = builder.AddCodeFromFile(tmp);
            Assert.Equal(ScriptLanguage.CSharp, builder.ScriptCode.Language);
            Assert.Contains("sample code", builder.ScriptCode.Code);
        }
        finally
        {
            if (File.Exists(tmp))
            {
                File.Delete(tmp);
            }
        }
    }

    [Fact]
    public void AddOpenApiTag_AddsTagsAcrossVerbs()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/tags", [HttpVerb.Get, HttpVerb.Post]);
        _ = builder.AddOpenApiTag(["Users", "Admin"]);
        foreach (var v in builder.HttpVerbs)
        {
            Assert.True(builder.OpenAPI.ContainsKey(v));
            Assert.Contains("Users", builder.OpenAPI[v].Tags);
            Assert.Contains("Admin", builder.OpenAPI[v].Tags);
        }
    }

    [Fact]
    public void AddOpenApiServer_PathLevel()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/server", [HttpVerb.Get]);
        _ = builder.AddOpenApiServer([new Microsoft.OpenApi.OpenApiServer { Url = "https://api" }]);
        Assert.NotNull(builder.PathLevelOpenAPIMetadata);
        _ = Assert.Single(builder.PathLevelOpenAPIMetadata!.Servers!);
        Assert.Equal("https://api", builder.PathLevelOpenAPIMetadata.Servers![0].Url);
    }

    [Fact]
    public void AddOpenApiInfo_VerbLevel_OperationIdAndSummary()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/info", [HttpVerb.Get]);
        _ = builder.AddOpenApiInfo(summary: "Get info", description: "Detailed", operationId: "GetInfo", deprecated: true);
        var meta = builder.OpenAPI[HttpVerb.Get];
        Assert.Equal("GetInfo", meta.OperationId);
        Assert.Equal("Get info", meta.Summary);
        Assert.Equal("Detailed", meta.Description);
        Assert.True(meta.Deprecated);
    }

    [Fact]
    public void AddOpenApiExternalDoc_AddsDocs()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/ext", [HttpVerb.Post]);
        _ = builder.AddOpenApiExternalDoc(new Uri("https://docs"), "More info");
        var meta = builder.OpenAPI[HttpVerb.Post];
        Assert.NotNull(meta.ExternalDocs);
        Assert.Equal("More info", meta.ExternalDocs.Description);
        // Uri normalization appends trailing slash
        Assert.Equal("https://docs/", meta.ExternalDocs.Url!.ToString());
    }

    [Fact]
    public void AddOpenApiResponse_DescriptionOnly()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/resp", [HttpVerb.Get]);
        _ = builder.AddOpenApiResponse("200", "OK description");
        var meta = builder.OpenAPI[HttpVerb.Get];
        Assert.NotNull(meta.Responses);
        var resp = meta.Responses["200"];
        Assert.Equal("OK description", resp.Description);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiResponse>(resp);
    }

    [Fact]
    public void AddOpenApiResponse_FromReference_EmbedVsRef()
    {
        var host = NewHost();
        var doc = host.GetOrCreateOpenApiDocument(IOpenApiAuthenticationOptions.DefaultSchemeName);
        doc.GenerateComponents(new OpenApiComponentSet { ResponseTypes = [typeof(ResponseComponentHolder)] });
        var builder = MapRouteBuilder.Create(host, "/respref", [HttpVerb.Post]);
        _ = builder.AddOpenApiResponseFromReference("200", "Ok", description: "Override desc", embed: false);
        var meta = builder.OpenAPI[HttpVerb.Post];
        var respRef = meta.Responses!["200"];
        Assert.Equal("Override desc", respRef.Description);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiResponseReference>(respRef);

        // Embedded
        var builderEmbed = MapRouteBuilder.Create(host, "/respembed", [HttpVerb.Post]);
        _ = builderEmbed.AddOpenApiResponseFromReference("200", "Ok", description: "Embed desc", embed: true);
        var respEmbed = builderEmbed.OpenAPI[HttpVerb.Post].Responses!["200"];
        Assert.Equal("Embed desc", respEmbed.Description);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiResponse>(respEmbed);
    }

    [Fact]
    public void AddOpenApiParameter_ReferenceAndEmbed()
    {
        var host = NewHost();
        var doc = host.GetOrCreateOpenApiDocument(IOpenApiAuthenticationOptions.DefaultSchemeName);
        doc.GenerateComponents(new OpenApiComponentSet { ParameterTypes = [typeof(ParameterComponentHolder)] });
        var builder = MapRouteBuilder.Create(host, "/param/{id}", [HttpVerb.Get]);
        _ = builder.AddOpenApiParameter("Id", verbs: [HttpVerb.Get], description: "override desc", embed: false);
        var meta = builder.OpenAPI[HttpVerb.Get];
        _ = Assert.Single(meta.Parameters!);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiParameterReference>(meta.Parameters![0]);
        Assert.Equal("override desc", meta.Parameters![0].Description);

        var builderEmbed = MapRouteBuilder.Create(host, "/param2/{id}", [HttpVerb.Get]);
        _ = builderEmbed.AddOpenApiParameter("Id", verbs: [HttpVerb.Get], embed: true, key: "id2");
        var metaEmbed = builderEmbed.OpenAPI[HttpVerb.Get];
        Assert.Equal("id2", metaEmbed.Parameters![0].Name);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiParameter>(metaEmbed.Parameters![0]);
    }

    [Fact]
    public void AddOpenApiRequestBody_Reference_EmbedVsRefAndForce()
    {
        var host = NewHost();
        var doc = host.GetOrCreateOpenApiDocument(IOpenApiAuthenticationOptions.DefaultSchemeName);
        doc.GenerateComponents(new OpenApiComponentSet { RequestBodyTypes = [typeof(RequestBodyComponentHolder)] });
        // POST normal
        var builderPost = MapRouteBuilder.Create(host, "/rb", [HttpVerb.Post]);
        _ = builderPost.AddOpenApiRequestBody("ReqBody", description: "desc", embed: false);
        var rbRef = builderPost.OpenAPI[HttpVerb.Post].RequestBody;
        Assert.Equal("desc", rbRef!.Description);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiRequestBodyReference>(rbRef);

        // Embedded
        var builderPostEmbed = MapRouteBuilder.Create(host, "/rbembed", [HttpVerb.Post]);
        _ = builderPostEmbed.AddOpenApiRequestBody("ReqBody", embed: true);
        _ = Assert.IsType<Microsoft.OpenApi.OpenApiRequestBody>(builderPostEmbed.OpenAPI[HttpVerb.Post].RequestBody);

        // GET requires force
        var builderGet = MapRouteBuilder.Create(host, "/rbget", [HttpVerb.Get]);
        _ = builderGet.AddOpenApiRequestBody("ReqBody", force: true);
        Assert.NotNull(builderGet.OpenAPI[HttpVerb.Get].RequestBody);
    }

    [Fact]
    public void AddAuthorization_AddsPoliciesAndSchemes()
    {
        var host = NewHost();
        // Simulate authentication registrations (depends on host implementation). We'll assume no schemes returned => only explicit schema captured.
        var builder = MapRouteBuilder.Create(host, "/auth", [HttpVerb.Get]);
        _ = builder.AddAuthorization(policies: ["PolicyA", "PolicyB"], schema: "Scheme1");
        Assert.Contains("Scheme1", builder.RequireSchemes);
        Assert.Contains("PolicyA", builder.RequirePolicies);
        Assert.Contains("PolicyB", builder.RequirePolicies);
        var meta = builder.OpenAPI[HttpVerb.Get];
        Assert.NotNull(meta.Security);
        Assert.True(meta.Security.Count > 0);
        Assert.Contains(meta.Security.First().Keys, k => k == "Scheme1");
    }

    [Fact]
    public void AddOpenApiTag_Empty_Throws()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/tags-empty", [HttpVerb.Get]);
        var ex = Assert.Throws<ArgumentException>(() => builder.AddOpenApiTag([]));
        Assert.Contains("At least one non-empty tag", ex.Message);
    }

    [Fact]
    public void AddOpenApiRequestBody_GetWithoutForce_Skips()
    {
        var host = NewHost();
        var doc = host.GetOrCreateOpenApiDocument(IOpenApiAuthenticationOptions.DefaultSchemeName);
        doc.GenerateComponents(new OpenApiComponentSet { RequestBodyTypes = [typeof(RequestBodyComponentHolder)] });
        var builder = MapRouteBuilder.Create(host, "/get-skip", [HttpVerb.Get]);
        _ = builder.AddOpenApiRequestBody("ReqBody", force: false);
        // When force=false for GET, metadata for GET may not be created; ensure dictionary does not contain verb or has null request body
        if (builder.OpenAPI.TryGetValue(HttpVerb.Get, out var meta))
        {
            Assert.Null(meta.RequestBody);
        }
        else
        {
            Assert.False(builder.OpenAPI.ContainsKey(HttpVerb.Get));
        }
    }

    [Fact]
    public void AddOpenApiRequestBody_Duplicate_Throws()
    {
        var host = NewHost();
        var doc = host.GetOrCreateOpenApiDocument(IOpenApiAuthenticationOptions.DefaultSchemeName);
        doc.GenerateComponents(new OpenApiComponentSet { RequestBodyTypes = [typeof(RequestBodyComponentHolder)] });
        var builder = MapRouteBuilder.Create(host, "/dup", [HttpVerb.Post]);
        _ = builder.AddOpenApiRequestBody("ReqBody");
        var ex = Assert.Throws<InvalidOperationException>(() => builder.AddOpenApiRequestBody("ReqBody"));
        Assert.Contains("already defined", ex.Message);
    }

    [Fact]
    public void AddAuthorization_MultipleCalls_MergesSchemesAndPolicies()
    {
        var host = NewHost();
        var builder = MapRouteBuilder.Create(host, "/auth-multi", [HttpVerb.Get, HttpVerb.Post]);
        _ = builder.AddAuthorization(policies: ["PolicyA"], schema: "Scheme1", verbs: [HttpVerb.Get]);
        _ = builder.AddAuthorization(policies: ["PolicyB"], schema: "Scheme2", verbs: [HttpVerb.Post]);
        Assert.Contains("Scheme1", builder.RequireSchemes);
        Assert.Contains("Scheme2", builder.RequireSchemes);
        Assert.Contains("PolicyA", builder.RequirePolicies);
        Assert.Contains("PolicyB", builder.RequirePolicies);
        var getMeta = builder.OpenAPI[HttpVerb.Get];
        var postMeta = builder.OpenAPI[HttpVerb.Post];
        Assert.Contains(getMeta.Security!.First().Keys, k => k == "Scheme1");
        Assert.Contains(postMeta.Security!.First().Keys, k => k == "Scheme2");
    }
}

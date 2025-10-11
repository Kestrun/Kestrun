using Kestrun.Languages;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using Serilog;
using Xunit;

namespace KestrunTests.Languages;

public class VBNetDelegateBuilderTests
{
    [Fact]
    [Trait("Category", "Languages")]
    public async Task Build_ExecutesWrappedScript_AndAppliesDefaultResponse()
    {
        var log = new Mock<ILogger>(MockBehavior.Loose).Object;

        // Minimal VB body that compiles — doesn’t need to touch Response
        var userCode = "Dim a As Integer = 1\r\nReturn True";
        var host = new KestrunHost("Tests", Log.Logger);
        var del = VBNetDelegateBuilder.Build(host, userCode, args: null, extraImports: null, extraRefs: null);

        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/";

        await del(http);

        Assert.Equal(200, http.Response.StatusCode);
        Assert.Null(http.Response.ContentType);
    }
}

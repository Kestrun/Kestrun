using Kestrun.Languages;
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
        var del = VBNetDelegateBuilder.Build(userCode, log, args: null, extraImports: null, extraRefs: null);

        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/";

        await del(http);

        Assert.Equal(200, http.Response.StatusCode);
        Assert.Equal("text/plain; charset=utf-8", http.Response.ContentType);
    }
}

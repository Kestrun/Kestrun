using System.Reflection;
using Xunit;

namespace KestrunTests.Callback;

public class KestrunContextKeysTests
{
    [Fact]
    public void Keys_AreStableStrings()
    {
        var asm = typeof(Kestrun.Callback.CallbackRequest).Assembly;
        var t = asm.GetType("Kestrun.Callback.KestrunContextKeys", throwOnError: true)!;

        var requestBodyKey = (string)t.GetField("RequestBodyJsonElement", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var payloadKey = (string)t.GetField("CallbackPayload", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        Assert.Equal("kestrun.requestBody.jsonElement", requestBodyKey);
        Assert.Equal("kestrun.callback.payload", payloadKey);
    }
}

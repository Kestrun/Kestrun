using Kestrun.Forms;
using Kestrun.Languages;
using Serilog;
using Xunit;

namespace KestrunTests.Languages;

public sealed class ParameterForInjectionInfoFormPayloadTests
{
    [Fact]
    public void CoerceFormPayloadForParameter_CreatesDerivedMultipartInstance()
    {
        var payload = new KrMultipart();
        payload.Parts.Add(new KrRawPart { TempPath = "temp" });

        var result = ParameterForInjectionInfo.CoerceFormPayloadForParameter(typeof(CustomMultipart), payload, Log.Logger);

        var converted = Assert.IsType<CustomMultipart>(result);
        Assert.Single(converted.Parts);
        Assert.Same(payload.Parts[0], converted.Parts[0]);
    }

    private sealed class CustomMultipart : KrMultipart
    {
    }
}

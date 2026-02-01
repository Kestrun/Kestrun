using Kestrun.Forms;
using Kestrun.Languages;
using Serilog;
using Xunit;

namespace KestrunTests.Languages;

public sealed class ParameterForInjectionInfoFormDataPayloadTests
{
    [Fact]
    public void CoerceFormPayloadForParameter_CreatesDerivedFormDataInstance()
    {
        var payload = new KrFormData();
        payload.Fields["note"] = ["hello"];
        payload.Files["file"] =
        [
            new KrFilePart
            {
                Name = "file",
                OriginalFileName = "hello.txt",
                TempPath = "temp"
            }
        ];

        var result = ParameterForInjectionInfo.CoerceFormPayloadForParameter(typeof(CustomFormData), payload, Log.Logger);

        var converted = Assert.IsType<CustomFormData>(result);
        Assert.Equal("hello", converted.Fields["note"][0]);
        Assert.Single(converted.Files["file"]);
        Assert.Equal("hello.txt", converted.Files["file"][0].OriginalFileName);
    }

    private sealed class CustomFormData : KrFormData
    {
    }
}

using Kestrun.Forms;
using Xunit;

namespace KestrunTests.Forms;

public sealed class FormHelperApplyKrBindFormAttributeTests
{
    [Fact]
    [Trait("Category", "Forms")]
    public void ApplyKrPartAttributes_CopiesKrBindFormSettingsIntoOptions()
    {
        var attr = new KrBindFormAttribute
        {
            ComputeSha256 = true,
            EnablePartDecompression = true,
            RejectUnknownRequestContentType = false,
            RejectUnknownContentEncoding = false,
            DefaultUploadPath = "./uploads",
            MaxDecompressedBytesPerPart = 123,
            AllowedPartContentEncodings = ["gzip"],

            MaxRequestBodyBytes = 456,
            MaxPartBodyBytes = 789,
            MaxParts = 10,
            MaxHeaderBytesPerPart = 11,
            MaxFieldValueBytes = 12,
            MaxNestingDepth = 3
        };

        var options = FormHelper.ApplyKrPartAttributes(attr);

        Assert.True(options.ComputeSha256);
        Assert.True(options.EnablePartDecompression);
        Assert.False(options.RejectUnknownRequestContentType);
        Assert.False(options.RejectUnknownContentEncoding);
        Assert.Equal("./uploads", options.DefaultUploadPath);
        Assert.Equal(123, options.MaxDecompressedBytesPerPart);

        _ = Assert.Single(options.AllowedPartContentEncodings);
        Assert.Contains("gzip", options.AllowedPartContentEncodings);

        Assert.Equal(456, options.Limits.MaxRequestBodyBytes);
        Assert.Equal(789, options.Limits.MaxPartBodyBytes);
        Assert.Equal(10, options.Limits.MaxParts);
        Assert.Equal(11, options.Limits.MaxHeaderBytesPerPart);
        Assert.Equal(12, options.Limits.MaxFieldValueBytes);
        Assert.Equal(3, options.Limits.MaxNestingDepth);
    }
}

using Kestrun.Forms;
using Xunit;

namespace KestrunTests.Forms;

public class KrFormLimitsTests
{
    [Fact]
    [Trait("Category", "Forms")]
    public void CopyConstructor_CopiesAllValues()
    {
        var original = new KrFormLimits
        {
            MaxRequestBodyBytes = 123,
            MaxPartBodyBytes = 456,
            MaxParts = 7,
            MaxHeaderBytesPerPart = 89,
            MaxFieldValueBytes = 1011,
            MaxNestingDepth = 3
        };

        var copy = new KrFormLimits(original);

        Assert.Equal(original.MaxRequestBodyBytes, copy.MaxRequestBodyBytes);
        Assert.Equal(original.MaxPartBodyBytes, copy.MaxPartBodyBytes);
        Assert.Equal(original.MaxParts, copy.MaxParts);
        Assert.Equal(original.MaxHeaderBytesPerPart, copy.MaxHeaderBytesPerPart);
        Assert.Equal(original.MaxFieldValueBytes, copy.MaxFieldValueBytes);
        Assert.Equal(original.MaxNestingDepth, copy.MaxNestingDepth);
    }

    [Fact]
    [Trait("Category", "Forms")]
    public void ToString_ContainsKeySettings()
    {
        var limits = new KrFormLimits
        {
            MaxRequestBodyBytes = 1,
            MaxPartBodyBytes = 2,
            MaxParts = 3,
            MaxHeaderBytesPerPart = 4,
            MaxFieldValueBytes = 5,
            MaxNestingDepth = 6
        };

        var text = limits.ToString();

        Assert.Contains("MaxRequestBodyBytes=1", text);
        Assert.Contains("MaxPartBodyBytes=2", text);
        Assert.Contains("MaxParts=3", text);
        Assert.Contains("MaxHeaderBytesPerPart=4", text);
        Assert.Contains("MaxFieldValueBytes=5", text);
        Assert.Contains("MaxNestingDepth=6", text);
    }
}

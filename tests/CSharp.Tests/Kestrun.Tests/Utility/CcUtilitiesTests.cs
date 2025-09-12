using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utility;

public class CcUtilitiesTests
{
    [Theory]
    [InlineData("", 0, 1)]
    [InlineData("one", 0, 1)]
    [InlineData("one\nTwo", 3, 1)]
    [InlineData("one\nTwo", 4, 2)]
    [InlineData("a\nb\nc", 4, 3)]
    public void GetLineNumber_ComputesExpectedLine(string text, int index, int expected)
    {
        var line = CcUtilities.GetLineNumber(text, index);
        Assert.Equal(expected, line);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void GetLineNumber_Throws_OnOutOfRange(int index)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CcUtilities.GetLineNumber("abc", index));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void PreviewFeaturesEnabled_ReflectsAppContextSwitch()
    {
        // Ensure default is false when switch not set
        var before = CcUtilities.PreviewFeaturesEnabled();

        // Toggle switch on and verify
        AppContext.SetSwitch("System.Runtime.EnablePreviewFeatures", true);
        Assert.True(CcUtilities.PreviewFeaturesEnabled());

        // Restore prior state
        AppContext.SetSwitch("System.Runtime.EnablePreviewFeatures", before);
    }
}

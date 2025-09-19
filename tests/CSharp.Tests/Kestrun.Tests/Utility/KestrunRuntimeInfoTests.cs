using Xunit;

namespace KestrunTests.Utility;

public class KestrunRuntimeInfoTests
{
    [Fact]
    [Trait("Category", "Utility")]
    public void BuiltTargetFrameworkVersion_IsNonZero()
    {
        var ver = Kestrun.KestrunRuntimeInfo.GetBuiltTargetFrameworkVersion();
        // Library targets net8.0;net9.0 so either 8.x or 9.x depending on compiled TFM
        Assert.True(ver.Major >= 8, $"Expected major >= 8 but got {ver}");
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void BuiltTargetFrameworkName_IsNotUnknown()
    {
        var name = Kestrun.KestrunRuntimeInfo.GetBuiltTargetFrameworkName();
        Assert.False(string.Equals(name, "Unknown", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(".NETCoreApp", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void TryGetMinVersion_KnownFeature_Http3()
    {
        Assert.True(Kestrun.KestrunRuntimeInfo.TryGetMinVersion(nameof(Kestrun.KestrunRuntimeInfo.KnownFeature.Http3), out var min));
        Assert.Equal(8, min.Major);
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void Supports_UnknownFeature_False() => Assert.False(Kestrun.KestrunRuntimeInfo.Supports("TotallyUnknownFeatureName"));

}

using System;
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
    public void BuiltTargetFrameworkMoniker_IsNotUnknown()
    {
        var moniker = Kestrun.KestrunRuntimeInfo.GetBuiltTargetFrameworkMoniker();
        Assert.False(string.Equals(moniker, "Unknown", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(".NETCoreApp", moniker);
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
    public void Supports_UnknownFeature_False()
    {
        Assert.False(Kestrun.KestrunRuntimeInfo.Supports("TotallyUnknownFeatureName"));
    }

    [Fact]
    [Trait("Category", "Utility")]
    public void RegisterOrUpdateFeature_DynamicGating_Works()
    {
        var featureName = "ExperimentalX";
        // Register requiring very high major so initial Supports should be false.
        Kestrun.KestrunRuntimeInfo.RegisterOrUpdateFeature(featureName, new Version(99, 0));
        Assert.False(Kestrun.KestrunRuntimeInfo.Supports(featureName));

        // Now lower requirement to current major (derived from built version) so Supports becomes true.
        var built = Kestrun.KestrunRuntimeInfo.GetBuiltTargetFrameworkVersion();
        Kestrun.KestrunRuntimeInfo.RegisterOrUpdateFeature(featureName, new Version(built.Major, 0));
        Assert.True(Kestrun.KestrunRuntimeInfo.Supports(featureName));
    }
}

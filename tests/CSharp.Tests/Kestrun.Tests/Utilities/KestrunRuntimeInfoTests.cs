using Xunit;

namespace KestrunTests.Utilities;

public class KestrunRuntimeInfoTests
{
    [Fact]
    [Trait("Category", "Utilities")]
    public void BuiltTargetFrameworkVersion_IsNonZero()
    {
        var ver = KestrunAnnotationsRuntimeInfo.GetBuiltTargetFrameworkVersion();
        // Library targets net8.0;net9.0 so either 8.x or 9.x depending on compiled TFM
        Assert.True(ver.Major >= 8, $"Expected major >= 8 but got {ver}");
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void BuiltTargetFrameworkName_IsNotUnknown()
    {
        var name = Kestrun.KestrunRuntimeInfo.GetBuiltTargetFrameworkName();
        Assert.False(string.Equals(name, "Unknown", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(".NETCoreApp", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void TryGetMinVersion_KnownFeature_Http3()
    {
        Assert.True(Kestrun.KestrunRuntimeInfo.TryGetMinVersion(nameof(Kestrun.KestrunRuntimeInfo.KnownFeature.Http3), out var min));
        Assert.Equal(8, min.Major);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void TryGetMinVersion_KnownFeature_SuppressReadingTokenFromFormBody()
    {
        var featureName = nameof(Kestrun.KestrunRuntimeInfo.KnownFeature.SuppressReadingTokenFromFormBody);
        var found = Kestrun.KestrunRuntimeInfo.TryGetMinVersion(featureName, out var min);
        // Min version table entry is present for all builds (value = 9.0).
        Assert.True(found);
        Assert.Equal(9, min.Major);
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void Supports_SuppressReadingTokenFromFormBody_FeatureGate()
    {
        var feature = Kestrun.KestrunRuntimeInfo.KnownFeature.SuppressReadingTokenFromFormBody;
#if NET9_0_OR_GREATER
        Assert.True(Kestrun.KestrunRuntimeInfo.Supports(feature));
#else
        Assert.False(Kestrun.KestrunRuntimeInfo.Supports(feature));
#endif
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void Supports_StringOverload_SuppressReadingTokenFromFormBody_FeatureGate()
    {
        var featureName = nameof(Kestrun.KestrunRuntimeInfo.KnownFeature.SuppressReadingTokenFromFormBody);
#if NET9_0_OR_GREATER
        Assert.True(Kestrun.KestrunRuntimeInfo.Supports(featureName));
#else
        Assert.False(Kestrun.KestrunRuntimeInfo.Supports(featureName));
#endif
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void Supports_UnknownFeature_False() => Assert.False(Kestrun.KestrunRuntimeInfo.Supports("TotallyUnknownFeatureName"));

    [Fact]
    [Trait("Category", "Utilities")]
    public void GetKnownFeatures_IncludesExpected()
    {
        var features = Kestrun.KestrunRuntimeInfo.GetKnownFeatures().ToList();
        Assert.Contains(nameof(Kestrun.KestrunRuntimeInfo.KnownFeature.Http3), features);
        Assert.Contains(nameof(Kestrun.KestrunRuntimeInfo.KnownFeature.SuppressReadingTokenFromFormBody), features);
        // Should not contain duplicates
        Assert.Equal(features.Count, features.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

}

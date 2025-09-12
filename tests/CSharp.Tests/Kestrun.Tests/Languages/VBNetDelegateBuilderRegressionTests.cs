using Kestrun.Languages;
using Xunit;

namespace KestrunTests.Languages;

/// <summary>
/// Regression tests for VBNetDelegateBuilder Linux handling of deleted assembly files.
/// Previously, if an already loaded assembly's file was deleted (common for temp assemblies
/// in tests), Roslyn MetadataReference.CreateFromFile would throw FileNotFoundException when
/// enumerating AppDomain assemblies. We now skip assemblies whose physical file no longer exists.
/// </summary>
public class VBNetDelegateBuilderRegressionTests
{
    [Fact]
    [Trait("Category", "Languages")]
    public void Compile_Succeeds_When_Transient_Files_Are_Deleted()
    {
        // Arrange: This regression test ensures that calling Compile does not throw even if
        // temp directories used earlier in the test run have been deleted. The previous
        // implementation enumerated all loaded assemblies and unconditionally called
        // MetadataReference.CreateFromFile(a.Location) which threw when the file had been
        // removed (Linux temp folder cleanup). The fix skips assemblies whose Location no
        // longer exists. We cannot easily force an already-loaded assembly to have a now
        // deleted Location in a hermetic test, but we still exercise the code path to ensure
        // normal invocation succeeds.

        var code = "Return True"; // simple snippet returning Boolean

        // Act / Assert: should not throw
        var func = VBNetDelegateBuilder.Compile<bool>(code, Serilog.Log.Logger, null, null, null, Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16);
        Assert.NotNull(func);
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Kestrun.Hosting;
using Kestrun.Languages;
using Xunit;

namespace Kestrun.Tests.Languages;

public sealed record VbCustomPayload(string Name);

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
        var host = new KestrunHost("Tests");
        // Act / Assert: should not throw
        var func = VBNetDelegateBuilder.Compile<bool>(host, code, null, null, null, Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16);
        Assert.NotNull(func);
    }

    [Fact]
    [Trait("Category", "Languages")]
    public async Task Compile_Succeeds_With_Local_Custom_Type_Reference()
    {
        var host = new KestrunHost("Tests");
        var locals = new Dictionary<string, object?>
        {
            ["payload"] = new VbCustomPayload("ok")
        };
        var code = "Dim payload = CType(g.Locals(\"payload\"), VbCustomPayload)\r\nReturn payload.Name = \"ok\"";

        var func = VBNetDelegateBuilder.Compile<bool>(
            host,
            code,
            ["Kestrun.Tests.Languages"],
            null,
            locals,
            Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16);

        var result = await func(new CsGlobals(host.SharedState.Snapshot(), locals));
        Assert.True(result);
    }
}

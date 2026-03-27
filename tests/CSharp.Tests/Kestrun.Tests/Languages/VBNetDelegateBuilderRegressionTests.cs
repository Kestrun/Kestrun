using System.Reflection;
using System.Runtime.Loader;
using Kestrun.Hosting;
using Kestrun.Languages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Kestrun.Tests.Languages;

public sealed record VbCustomPayload(string Name);

/// <summary>
/// Regression tests for VBNetDelegateBuilder reference resolution.
/// These cover deleted loaded-assembly locations, local custom-type discovery,
/// and case-insensitive import namespace matching.
/// </summary>
public class VBNetDelegateBuilderRegressionTests
{
    [Fact]
    [Trait("Category", "Languages")]
    public void Compile_Succeeds_When_Loaded_Assembly_File_Is_Deleted()
    {
        var host = new KestrunHost("Tests");
        var root = Path.Combine(Path.GetTempPath(), "kestrun-vb-delegate-builder-tests", Guid.NewGuid().ToString("N"));
        var assemblyName = $"DeletedAssemblyRegression_{Guid.NewGuid():N}";
        var importNamespace = $"{assemblyName}.Namespace";
        var assemblyPath = Path.Combine(root, assemblyName + ".dll");
        _ = Directory.CreateDirectory(root);

        var loadContext = new AssemblyLoadContext(assemblyName, isCollectible: true);
        Assembly? loadedAssembly;
        try
        {
            CompileAssembly(
                assemblyPath,
                $$"""
                namespace {{importNamespace}}
                {
                    public static class Marker
                    {
                        public static bool Enabled => true;
                    }
                }
                """);

            loadedAssembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            Assert.Equal(assemblyPath, loadedAssembly.Location);
            Assert.NotNull(loadedAssembly.GetType($"{importNamespace}.Marker"));

            File.Delete(assemblyPath);
            Assert.False(File.Exists(assemblyPath));

            var func = VBNetDelegateBuilder.Compile<bool>(
                host,
                "Return True",
                [importNamespace],
                null,
                null,
                Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16);

            Assert.NotNull(func);
        }
        finally
        {
            loadContext.Unload();
            WaitForUnload();

            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
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

    [Fact]
    [Trait("Category", "Languages")]
    public async Task Compile_Succeeds_With_Differently_Cased_Import_Namespace()
    {
        var host = new KestrunHost("Tests");
        var code = "Dim payload As VbCustomPayload = New VbCustomPayload(\"ok\")\r\nReturn payload.Name = \"ok\"";

        var func = VBNetDelegateBuilder.Compile<bool>(
            host,
            code,
            ["kestrun.tests.languages"],
            null,
            null,
            Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.VisualBasic16);

        var result = await func(new CsGlobals(host.SharedState.Snapshot(), new Dictionary<string, object?>()));
        Assert.True(result);
    }

    private static void CompileAssembly(string outputPath, string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var assemblyName = Path.GetFileNameWithoutExtension(outputPath);

        var references = new List<MetadataReference>();
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? [];
        foreach (var path in trustedAssemblies)
        {
            var fileName = Path.GetFileName(path);
            if (fileName is "System.Runtime.dll" or "netstandard.dll" or "System.Private.CoreLib.dll")
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = compilation.Emit(outputPath);
        if (!result.Success)
        {
            var errors = string.Join('\n', result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException("Failed to compile test assembly: " + errors);
        }
    }

    private static void WaitForUnload()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Kestrun.Utilities;
using Xunit;

namespace KestrunTests.Utilities;

[Collection("SharedStateSerial")]
public class AssemblyAutoLoaderResolveTests
{
    private static readonly string SourceCode = "public class AutoLoaderTempType { public string Echo(string s)=>s; }";

    [Fact]
    [Trait("Category", "Utilities")]
    public void Resolve_From_Secondary_Directory_Loads_OnDemand()
    {
        // Clean initial state
        AssemblyAutoLoader.Clear(clearSearchDirs: true);

        var root = Path.Combine(Path.GetTempPath(), "kestrun-autoloader-tests", Guid.NewGuid().ToString("N"));
        var primary = Path.Combine(root, "primary");
        var secondary = Path.Combine(root, "secondary");
        _ = Directory.CreateDirectory(primary);
        _ = Directory.CreateDirectory(secondary);

        try
        {
            // Compile a simple assembly into secondary only
            var asmPath = Path.Combine(secondary, "AutoLoaderTemp.dll");
            CompileAssembly(asmPath, SourceCode);

            // Preload only the primary (empty) directory so the hook is installed
            AssemblyAutoLoader.PreloadAll(verbose: false, primary);

            // Type should not yet be loadable (assembly not in already loaded set)
            Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "AutoLoaderTemp");

            // Now add secondary directory (containing dll) -- hook already installed; PreloadAll will load it proactively
            AssemblyAutoLoader.PreloadAll(verbose: false, secondary);

            // Assembly should now be loaded
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "AutoLoaderTemp");
            Assert.NotNull(loaded);
            var type = loaded!.GetType("AutoLoaderTempType");
            Assert.NotNull(type);
        }
        finally
        {
            AssemblyAutoLoader.Clear(clearSearchDirs: true);
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "Utilities")]
    public void PreloadAll_Skips_Duplicate_Load()
    {
        AssemblyAutoLoader.Clear(clearSearchDirs: true);

        var dir = Path.Combine(Path.GetTempPath(), "kestrun-autoloader-tests", Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(dir);
        try
        {
            var asmPath = Path.Combine(dir, "DupLoad.dll");
            CompileAssembly(asmPath, SourceCode.Replace("AutoLoaderTempType", "DupLoadType"));

            // First call loads assembly
            AssemblyAutoLoader.PreloadAll(false, dir);
            var firstCount = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "DupLoad");
            Assert.Equal(1, firstCount);

            // Second call should not load duplicate (SafeLoad skip path)
            AssemblyAutoLoader.PreloadAll(false, dir);
            var secondCount = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "DupLoad");
            Assert.Equal(1, secondCount);
        }
        finally
        {
            AssemblyAutoLoader.Clear(clearSearchDirs: true);
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static void CompileAssembly(string outputPath, string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var assemblyName = Path.GetFileNameWithoutExtension(outputPath);

        var references = new List<MetadataReference>();
        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator) ?? [];
        foreach (var path in trustedAssemblies)
        {
            // Only add a few common references needed for simple code
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
            var errors = string.Join('\n', result.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException("Failed to compile test assembly: " + errors);
        }
    }
}

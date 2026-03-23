#if NET10_0_OR_GREATER
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using Xunit;

namespace Kestrun.Tool.Tests.Tooling;

public class KestrunToolModuleCommandBehaviorTests
{
    private static readonly Type ProgramType = ResolveProgramType();

    [Fact]
    [Trait("Category", "Tooling")]
    public void NormalizeRequestedModuleVersion_TrimsOrReturnsNull()
    {
        var trimmed = Assert.IsType<string>(Invoke("NormalizeRequestedModuleVersion", " 1.2.3 "));
        var missing = Invoke("NormalizeRequestedModuleVersion", "   ");

        Assert.Equal("1.2.3", trimmed);
        Assert.Null(missing);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void BuildGalleryPackageUrl_UsesExpectedShape()
    {
        var latest = Assert.IsType<string>(Invoke("BuildGalleryPackageUrl", (object?)null));
        var specific = Assert.IsType<string>(Invoke("BuildGalleryPackageUrl", "1.2.3"));

        Assert.EndsWith("/package/Kestrun", latest, StringComparison.Ordinal);
        Assert.EndsWith("/package/Kestrun/1.2.3", specific, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryHandlePackageDownloadResponseStatus_NotFoundWithRequestedVersion_FailsWithMessage()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var values = new object?[] { response, "1.2.3", false, null, null, null };
        var success = InvokeBool("TryHandlePackageDownloadResponseStatus", values);

        Assert.False(success);
        Assert.Equal([], Assert.IsType<byte[]>(values[3]));
        Assert.Equal(string.Empty, values[4]?.ToString());
        Assert.Contains("version '1.2.3' was not found", values[5]?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryDownloadPackagePayload_EmptyContent_Fails()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        };

        var values = new object?[] { response, false, null, null };
        var success = InvokeBool("TryDownloadPackagePayload", values);

        Assert.False(success);
        Assert.Equal([], Assert.IsType<byte[]>(values[2]));
        Assert.Contains("empty", values[3]?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryDownloadPackagePayload_WithContent_Succeeds()
    {
        var payload = Encoding.UTF8.GetBytes("payload");
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
        };

        var values = new object?[] { response, false, null, null };
        var success = InvokeBool("TryDownloadPackagePayload", values);

        Assert.True(success);
        Assert.Equal(payload, Assert.IsType<byte[]>(values[2]));
        Assert.Equal(string.Empty, values[3]?.ToString());
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveDownloadedPackageVersion_UsesRequestedVersionFallback()
    {
        var packageBytes = CreatePackageZip([
            ("tools/Kestrun/Public/Invoke-Test.ps1", "Write-Output 'ok'"),
        ]);
        var values = new object?[] { packageBytes, "1.2.3", null, null };
        var success = InvokeBool("TryResolveDownloadedPackageVersion", values);

        Assert.True(success);
        Assert.Equal("1.2.3", values[2]?.ToString());
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryCollectModulePayloadEntries_EmptyArchive_Fails()
    {
        var emptyArchiveBytes = CreatePackageZip([]);
        using var stream = new MemoryStream(emptyArchiveBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var values = new object?[] { archive, null, null };
        var success = InvokeBool("TryCollectModulePayloadEntries", values);

        Assert.False(success);
        Assert.Equal("Package did not contain any module payload files.", values[2]?.ToString());
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryCollectModulePayloadEntries_WithToolsPayload_Succeeds()
    {
        var packageBytes = CreatePackageZip([
            ("tools/Kestrun/Kestrun.psd1", "@{ }"),
            ("tools/Kestrun/Public/Invoke-Test.ps1", "Write-Output 'ok'"),
            ("Kestrun.nuspec", "<package/>"),
        ]);

        using var stream = new MemoryStream(packageBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var values = new object?[] { archive, null, null };
        var success = InvokeBool("TryCollectModulePayloadEntries", values);

        Assert.True(success);
        var payloadEntries = Assert.IsAssignableFrom<System.Collections.IEnumerable>(values[1]);
        Assert.NotEmpty(payloadEntries.Cast<object>());
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveSafeStagingDestination_RejectsTraversal()
    {
        var staging = Path.Combine(Path.GetTempPath(), $"kestrun-staging-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(staging);
        try
        {
            var fullStaging = Path.GetFullPath(staging);
            var fullStagingWithSeparator = fullStaging + Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            var values = new object?[] { staging, fullStagingWithSeparator, "../evil.ps1", "../evil.ps1", comparison, null, null };
            var success = InvokeBool("TryResolveSafeStagingDestination", values);

            Assert.False(success);
            Assert.Contains("outside staging directory", values[6]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryResolveExtractedManifestPath_ResolvesManifest()
    {
        var staging = Path.Combine(Path.GetTempPath(), $"kestrun-staging-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(staging);
        try
        {
            var moduleFolder = Path.Combine(staging, "Kestrun");
            _ = Directory.CreateDirectory(moduleFolder);
            var manifestPath = Path.Combine(moduleFolder, "Kestrun.psd1");
            File.WriteAllText(manifestPath, "@{ }", Encoding.UTF8);

            var values = new object?[] { staging, null, null };
            var success = InvokeBool("TryResolveExtractedManifestPath", values);

            Assert.True(success);
            Assert.Equal(Path.GetFullPath(manifestPath), Path.GetFullPath(values[1]?.ToString() ?? string.Empty));
            Assert.Equal(string.Empty, values[2]?.ToString());
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryInstallExtractedModule_RespectsOverwriteFlag()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-module-install-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source");
            _ = Directory.CreateDirectory(source);
            var sourceManifest = Path.Combine(source, "Kestrun.psd1");
            File.WriteAllText(sourceManifest, "@{ }", Encoding.UTF8);

            var moduleRoot = Path.Combine(root, "modules");
            var destination = Path.Combine(moduleRoot, "1.2.3");
            _ = Directory.CreateDirectory(destination);

            var failValues = new object?[] { sourceManifest, moduleRoot, "1.2.3", false, false, null, null };
            var fail = InvokeBool("TryInstallExtractedModule", failValues);
            Assert.False(fail);
            Assert.Contains("already exists", failValues[6]?.ToString(), StringComparison.OrdinalIgnoreCase);

            var successValues = new object?[] { sourceManifest, moduleRoot, "1.2.3", false, true, null, null };
            var success = InvokeBool("TryInstallExtractedModule", successValues);
            Assert.True(success);
            Assert.True(File.Exists(successValues[5]?.ToString()));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryExtractModulePackage_InstallsFromZipPayload()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kestrun-module-extract-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        try
        {
            var packageBytes = CreatePackageZip([
                ("tools/Kestrun/Kestrun.psd1", "@{ }"),
                ("tools/Kestrun/Public/Invoke-Test.ps1", "Write-Output 'ok'"),
            ]);

            var values = new object?[] { packageBytes, "1.2.3", root, false, false, null, null };
            var success = InvokeBool("TryExtractModulePackage", values);

            Assert.True(success);
            var installedManifest = values[5]?.ToString();
            Assert.False(string.IsNullOrWhiteSpace(installedManifest));
            Assert.True(File.Exists(installedManifest));
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseModuleArguments_UpdateWithOptions_Succeeds()
    {
        var values = new object?[] { new[] { "module", "update", "--scope", "global", "--version", "1.2.3", "--force" }, 1, null, null };
        var success = InvokeBool("TryParseModuleArguments", values);

        Assert.True(success);
        Assert.NotNull(values[2]);
        Assert.Equal("ModuleUpdate", GetRecordValue(values[2]!, "Mode"));
        Assert.Equal("Global", GetRecordValue(values[2]!, "ModuleScope"));
        Assert.Equal("1.2.3", GetRecordValue(values[2]!, "ModuleVersion"));
        Assert.Equal("True", GetRecordValue(values[2]!, "ModuleForce"));
    }

    [Fact]
    [Trait("Category", "Tooling")]
    public void TryParseModuleArguments_InstallWithForce_Fails()
    {
        var values = new object?[] { new[] { "module", "install", "--force" }, 1, null, null };
        var success = InvokeBool("TryParseModuleArguments", values);

        Assert.False(success);
        Assert.Equal("Module install does not accept --force.", values[3]?.ToString());
    }

    private static Type ResolveProgramType()
    {
        var assembly = Assembly.Load("Kestrun.Tool");
        var programType = assembly.GetType("Kestrun.Tool.Program", throwOnError: false);
        Assert.NotNull(programType);
        return programType;
    }

    private static MethodInfo GetMethod(string name, object?[] arguments)
    {
        var candidates = ProgramType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal))
            .Where(method => method.GetParameters().Length == arguments.Length)
            .ToList();

        foreach (var candidate in candidates)
        {
            var parameters = candidate.GetParameters();
            var matched = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                var argument = arguments[i];
                if (argument is null)
                {
                    continue;
                }

                var parameterType = parameters[i].ParameterType;
                if (!parameterType.IsByRef && !parameterType.IsInstanceOfType(argument))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Unable to resolve method '{name}'.");
    }

    private static object? Invoke(string name, params object?[] arguments)
    {
        var method = GetMethod(name, arguments);
        return method.Invoke(null, arguments);
    }

    private static bool InvokeBool(string name, object?[] arguments)
    {
        var result = Invoke(name, arguments);
        Assert.NotNull(result);
        return Assert.IsType<bool>(result);
    }

    private static string GetRecordValue(object record, string propertyName)
    {
        var property = record.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property.GetValue(record)?.ToString() ?? string.Empty;
    }

    private static byte[] CreatePackageZip(IReadOnlyList<(string Path, string Content)> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var entry = archive.CreateEntry(path);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false);
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
#endif

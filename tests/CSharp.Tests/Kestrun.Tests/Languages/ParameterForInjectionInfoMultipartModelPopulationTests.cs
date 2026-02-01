using System;
using System.Collections;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Kestrun.Forms;
using Kestrun.Languages;
using Serilog;
using Xunit;

namespace KestrunTests.Languages;

public sealed class ParameterForInjectionInfoMultipartModelPopulationTests
{
    [Fact]
    public void CoerceFormPayloadForParameter_PopulatesMultipartModelProperties_ForPowerShellDerivedTypes()
    {
        using var logger = new LoggerConfiguration().MinimumLevel.Verbose().CreateLogger();

        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();

        using var ps = PowerShell.Create();
        ps.Runspace = runspace;

        // Define PowerShell classes in the runspace.
        // NOTE: These mirror docs/_includes/examples/pwsh/22.XX-Nested-Multipart-OpenAPI.ps1
        var script = @"
using namespace Kestrun.Forms

class OuterControl {
  [object]$AdditionalProperties
}

class NestedParts {
  [string]$text
  [string]$json
}

class NestedMultipartRequest : KrMultipart {
  [OuterControl]$outer
  [NestedParts[]]$nested
}

[NestedMultipartRequest]
";

        ps.AddScript(script);
        var typeResult = ps.Invoke();
        Assert.False(ps.HadErrors);
        Assert.Single(typeResult);

        var nestedMultipartRequestType = Assert.IsAssignableFrom<Type>(typeResult[0].BaseObject);

        var outerJsonPath = CreateTempFile("{\"stage\":\"outer\"}");
        var innerTextPath = CreateTempFile("inner-1");
        var innerJsonPath = CreateTempFile("{\"nested\":true}");
        var outerNestedContainerPath = CreateTempFile("(container)");

        try
        {
            var innerPayload = new KrMultipart();
            innerPayload.Parts.Add(new KrRawPart { Name = "text", ContentType = "text/plain", TempPath = innerTextPath });
            innerPayload.Parts.Add(new KrRawPart { Name = "json", ContentType = "application/json", TempPath = innerJsonPath });

            var outerPayload = new KrMultipart();
            outerPayload.Parts.Add(new KrRawPart { Name = "outer", ContentType = "application/json", TempPath = outerJsonPath });
            outerPayload.Parts.Add(new KrRawPart
            {
                Name = "nested",
                ContentType = "multipart/mixed; boundary=inner-boundary",
                TempPath = outerNestedContainerPath,
                NestedPayload = innerPayload
            });

            var result = ParameterForInjectionInfo.CoerceFormPayloadForParameter(nestedMultipartRequestType, outerPayload, logger, ps);
            Assert.NotNull(result);

            var typed = Assert.IsAssignableFrom<KrMultipart>(result);
            Assert.Equal(2, typed.Parts.Count);

            // Validate OuterControl binding.
            var outerProp = typed.GetType().GetProperty("outer", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            Assert.NotNull(outerProp);

            var outerValue = outerProp!.GetValue(typed);
            Assert.NotNull(outerValue);

            var additionalProps = outerValue!.GetType().GetProperty("AdditionalProperties", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            Assert.NotNull(additionalProps);

            var additionalValue = additionalProps!.GetValue(outerValue);
            var ht = Assert.IsType<Hashtable>(additionalValue);
            Assert.Equal("outer", ht["stage"]);

            // Validate NestedParts[] binding.
            var nestedProp = typed.GetType().GetProperty("nested", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            Assert.NotNull(nestedProp);

            var nestedValue = nestedProp!.GetValue(typed);
            var nestedArr = Assert.IsAssignableFrom<Array>(nestedValue);
            Assert.Single(nestedArr);

            var nestedItem = nestedArr.GetValue(0);
            Assert.NotNull(nestedItem);

            var textProp = nestedItem!.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            var jsonProp = nestedItem.GetType().GetProperty("json", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            Assert.NotNull(textProp);
            Assert.NotNull(jsonProp);

            Assert.Equal("inner-1", textProp!.GetValue(nestedItem));
            Assert.Equal("{\"nested\":true}", jsonProp!.GetValue(nestedItem));
        }
        finally
        {
            SafeDeleteFile(outerJsonPath);
            SafeDeleteFile(innerTextPath);
            SafeDeleteFile(innerJsonPath);
            SafeDeleteFile(outerNestedContainerPath);
        }
    }

    private static string CreateTempFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), "kestrun-test-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, contents);
        return path;
    }

    private static void SafeDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }
}

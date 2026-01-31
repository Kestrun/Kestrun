using Kestrun.Forms;
using Kestrun.Hosting;
using Serilog;
using Xunit;

namespace KestrunTests.Forms;

public sealed class KrFormOptionsNestedRulesTests
{
    [Fact]
    public void AddFormOption_PopulatesNestedRules_FromScopedRules()
    {
        using var host = new KestrunHost("Tests", Log.Logger);

        var options = new KrFormOptions { Name = "NestedForm" };
        options.Rules.AddRange(
        [
            new KrFormPartRule { Name = "outer", Required = true },
            new KrFormPartRule { Name = "nested", Required = true },
            new KrFormPartRule { Name = "text", Scope = "nested", Required = true },
            new KrFormPartRule { Name = "json", Scope = "nested", Required = true }
        ]);

        Assert.True(host.AddFormOption(options));

        var stored = host.GetFormOption("NestedForm");
        Assert.NotNull(stored);

        var container = Assert.Single(stored!.Rules, r => string.Equals(r.Name, "nested", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, container.NestedRules.Count);
        Assert.Contains(container.NestedRules, r => string.Equals(r.Name, "text", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(container.NestedRules, r => string.Equals(r.Name, "json", StringComparison.OrdinalIgnoreCase));
    }
}

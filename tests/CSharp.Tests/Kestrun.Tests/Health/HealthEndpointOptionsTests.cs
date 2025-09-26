using Kestrun.Health;
using Kestrun.Scripting;
using Xunit;

namespace KestrunTests.Health;

public class HealthEndpointOptionsTests
{
    [Fact]
    public void Clone_CopiesAllProperties()
    {
        var options = new HealthEndpointOptions
        {
            Pattern = "/custom",
            DefaultTags = ["core", "db"],
            AllowAnonymous = true,
            TreatDegradedAsUnhealthy = true,
            ThrowOnDuplicate = true,
            RequireSchemes = ["https"],
            RequirePolicies = ["admin"],
            CorsPolicyName = "cors",
            RateLimitPolicyName = "rate",
            ShortCircuit = true,
            ShortCircuitStatusCode = 418,
            OpenApiSummary = "summary",
            OpenApiDescription = "desc",
            OpenApiOperationId = "opid",
            OpenApiTags = ["tag1"],
            OpenApiGroupName = "group",
            MaxDegreeOfParallelism = 4,
            ProbeTimeout = TimeSpan.FromMilliseconds(123),
            AutoRegisterEndpoint = false,
            DefaultScriptLanguage = ScriptLanguage.PowerShell
        };
        var clone = options.Clone();
        Assert.Equal(options.Pattern, clone.Pattern);
        Assert.Equal(options.DefaultTags, clone.DefaultTags);
        Assert.Equal(options.AllowAnonymous, clone.AllowAnonymous);
        Assert.Equal(options.TreatDegradedAsUnhealthy, clone.TreatDegradedAsUnhealthy);
        Assert.Equal(options.ThrowOnDuplicate, clone.ThrowOnDuplicate);
        Assert.Equal(options.RequireSchemes, clone.RequireSchemes);
        Assert.Equal(options.RequirePolicies, clone.RequirePolicies);
        Assert.Equal(options.CorsPolicyName, clone.CorsPolicyName);
        Assert.Equal(options.RateLimitPolicyName, clone.RateLimitPolicyName);
        Assert.Equal(options.ShortCircuit, clone.ShortCircuit);
        Assert.Equal(options.ShortCircuitStatusCode, clone.ShortCircuitStatusCode);
        Assert.Equal(options.OpenApiSummary, clone.OpenApiSummary);
        Assert.Equal(options.OpenApiDescription, clone.OpenApiDescription);
        Assert.Equal(options.OpenApiOperationId, clone.OpenApiOperationId);
        Assert.Equal(options.OpenApiTags, clone.OpenApiTags);
        Assert.Equal(options.OpenApiGroupName, clone.OpenApiGroupName);
        Assert.Equal(options.MaxDegreeOfParallelism, clone.MaxDegreeOfParallelism);
        Assert.Equal(options.ProbeTimeout, clone.ProbeTimeout);
        Assert.Equal(options.AutoRegisterEndpoint, clone.AutoRegisterEndpoint);
        Assert.Equal(options.DefaultScriptLanguage, clone.DefaultScriptLanguage);
    }
}

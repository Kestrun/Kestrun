using System.Security.Claims;
using Kestrun.Claims;
using Xunit;

namespace KestrunTests.Claims;

public class ClaimPolicyBuilderTests
{
    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_WithString_AddsPolicy_WithValues()
    {
        var builder = new ClaimPolicyBuilder()
            .AddPolicy("Admin", "role", "admin", "owner");

        // Case-insensitive key lookup
        Assert.True(builder.Policies.ContainsKey("ADMIN"));

        var rule = builder.Policies["admin"];
        Assert.Equal("role", rule.ClaimType);
        Assert.Equal(["admin", "owner"], rule.AllowedValues);
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_WithEnum_Uses_ToClaimUri_Mapping()
    {
        var builder = new ClaimPolicyBuilder()
            .AddPolicy("RolePolicy", UserIdentityClaim.Role, "Manager");

        var rule = builder.Policies["RolePolicy"];
        Assert.Equal(ClaimTypes.Role, rule.ClaimType);
        Assert.Equal(["Manager"], rule.AllowedValues);
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_WithRule_AddsPolicy()
    {
        var rule = new ClaimRule(ClaimTypes.NameIdentifier, "42");
        var builder = new ClaimPolicyBuilder().AddPolicy("ById", rule);

        Assert.True(builder.Policies.TryGetValue("byid", out var fetched));
        Assert.Same(rule, fetched);
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_Throws_On_NullOrWhitespace_PolicyName()
    {
        var builder = new ClaimPolicyBuilder();
        // Null -> ArgumentNullException (a subtype of ArgumentException)
        _ = Assert.ThrowsAny<ArgumentException>(() => builder.AddPolicy(null!, "type", "v"));
        // Whitespace -> ArgumentException
        _ = Assert.Throws<ArgumentException>(() => builder.AddPolicy(" ", "type", "v"));

        var rule = new ClaimRule("type", "v");
        _ = Assert.ThrowsAny<ArgumentException>(() => builder.AddPolicy(null!, rule));
        _ = Assert.Throws<ArgumentException>(() => builder.AddPolicy("", rule));
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_Throws_On_NullOrWhitespace_ClaimType()
    {
        var builder = new ClaimPolicyBuilder();
        // Null -> ArgumentNullException (a subtype of ArgumentException)
        _ = Assert.ThrowsAny<ArgumentException>(() => builder.AddPolicy("P", null!, "v"));
        // Whitespace -> ArgumentException
        _ = Assert.Throws<ArgumentException>(() => builder.AddPolicy("P", " ", "v"));
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_Throws_On_EmptyOrNull_AllowedValues_For_StringOverload()
    {
        var builder = new ClaimPolicyBuilder();
        // No values
        _ = Assert.Throws<ArgumentException>(() => builder.AddPolicy("P", "type"));
        // Null array explicitly
        _ = Assert.Throws<ArgumentException>(() => builder.AddPolicy("P", "type", (string[])(object)null!));
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_Throws_On_EmptyOrNull_AllowedValues_For_EnumOverload()
    {
        var builder = new ClaimPolicyBuilder();
        _ = Assert.Throws<ArgumentException>(() => builder.AddPolicy("P", UserIdentityClaim.Name));
        _ = Assert.Throws<ArgumentException>(() => builder.AddPolicy("P", UserIdentityClaim.Name, (string[])(object)null!));
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void AddPolicy_Throws_On_Null_Rule()
    {
        var builder = new ClaimPolicyBuilder();
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddPolicy("P", (ClaimRule)null!));
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void Policies_AreCaseInsensitive_And_LastWriteWins()
    {
        var builder = new ClaimPolicyBuilder()
            .AddPolicy("PolicyX", "t1", "a")
            .AddPolicy("policyx", "t2", "b");

        Assert.True(builder.Policies.ContainsKey("POLICYX"));
        var rule = builder.Policies["PolicyX"];
        Assert.Equal("t2", rule.ClaimType); // overwritten
        Assert.Equal(["b"], rule.AllowedValues);
    }

    [Fact]
    [Trait("Category", "Claims")]
    public void Build_Returns_Independent_CaseInsensitive_Dictionary()
    {
        var builder = new ClaimPolicyBuilder()
            .AddPolicy("Admin", ClaimTypes.Role, "admin");

        var config = builder.Build();

        // Independence: subsequent changes to builder shouldn't affect config
        _ = builder.AddPolicy("Other", ClaimTypes.Name, "bob");

        Assert.True(config.Policies.ContainsKey("ADMIN")); // case-insensitive dictionary
        Assert.False(config.Policies.ContainsKey("Other"));

        var rule = config.Policies["admin"];
        Assert.Equal(ClaimTypes.Role, rule.ClaimType);
        Assert.Equal(["admin"], rule.AllowedValues);
    }
}

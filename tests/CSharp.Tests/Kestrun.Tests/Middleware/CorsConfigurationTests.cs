using Kestrun.Hosting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Serilog;
using Xunit;

namespace KestrunTests.Middleware;

public class CorsConfigurationTests
{
    [Fact]
    public void CorsPolicyDefined_InitiallyFalse()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Assert
        Assert.False(host.CorsPolicyDefined);
    }

    [Fact]
    public void AddCorsDefaultPolicy_WithBuilder_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);
        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader();

        // Act
        _ = host.AddCorsDefaultPolicy(builder);

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public void AddCorsDefaultPolicy_WithAction_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act
        _ = host.AddCorsDefaultPolicy(builder =>
        {
            _ = builder.WithOrigins("http://localhost:3000")
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public void AddCorsPolicy_WithNameAndBuilder_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);
        var builder = new CorsPolicyBuilder()
            .WithOrigins("http://localhost:3000")
            .AllowAnyMethod();

        // Act
        _ = host.AddCorsPolicy("MyPolicy", builder);

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public void AddCorsPolicy_WithNameAndAction_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act
        _ = host.AddCorsPolicy("MyPolicy", builder =>
        {
            _ = builder.WithOrigins("http://localhost:3000")
                   .AllowAnyMethod();
        });

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public void AddCorsDefaultPolicyAllowAll_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act
        _ = host.AddCorsDefaultPolicyAllowAll();

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public void AddCorsPolicyAllowAll_WithName_SetsCorsPolicyDefinedFlag()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act
        _ = host.AddCorsPolicyAllowAll("AllowAllPolicy");

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public void MultiplePolicies_AllSetFlag()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act - Add multiple policies
        _ = host.AddCorsPolicy("Policy1", builder => builder.WithOrigins("http://localhost:3000"));
        _ = host.AddCorsPolicy("Policy2", builder => builder.WithOrigins("http://localhost:4000"));
        _ = host.AddCorsPolicy("Policy3", builder => builder.WithOrigins("http://localhost:5000"));

        // Assert
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public void AddCorsPolicy_AfterAnotherPolicy_FlagRemainsTrue()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act
        _ = host.AddCorsDefaultPolicyAllowAll();
        Assert.True(host.CorsPolicyDefined);

        _ = host.AddCorsPolicy("AnotherPolicy", builder => builder.WithOrigins("http://localhost:3000"));

        // Assert - Flag should still be true
        Assert.True(host.CorsPolicyDefined);
    }

    [Fact]
    public void AddCorsPolicy_WithEmptyPolicyName_ThrowsArgumentException()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);
        var builder = new CorsPolicyBuilder().AllowAnyOrigin();

        // Act & Assert
        _ = Assert.Throws<ArgumentException>(() => host.AddCorsPolicy("", builder));
    }

    [Fact]
    public void AddCorsPolicy_WithNullPolicyName_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);
        var builder = new CorsPolicyBuilder().AllowAnyOrigin();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCorsPolicy(null!, builder));
    }

    [Fact]
    public void AddCorsPolicy_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCorsPolicy("TestPolicy", (CorsPolicyBuilder)null!));
    }

    [Fact]
    public void AddCorsPolicy_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCorsPolicy("TestPolicy", (Action<CorsPolicyBuilder>)null!));
    }

    [Fact]
    public void AddCorsDefaultPolicy_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCorsDefaultPolicy((CorsPolicyBuilder)null!));
    }

    [Fact]
    public void AddCorsDefaultPolicy_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestServer", logger);

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => host.AddCorsDefaultPolicy((Action<CorsPolicyBuilder>)null!));
    }
}

using System.Management.Automation;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Collections;
using System.Reflection;
using Moq;
using Serilog;
using Xunit;
using Kestrun.SharedState;
using Kestrun.Authentication;

namespace KestrunTests.Authentication;

[Collection("SharedStateSerial")]
public class IAuthHandlerTest
{
    [Fact]
    public async Task GetAuthenticationTicketAsync_AddsNameClaim_WhenMissing()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var loggerMock = new Mock<ILogger>();
        var optionsMock = new Mock<IAuthenticationCommonOptions>();
        _ = optionsMock.SetupGet(o => o.IssueClaims).Returns((Func<HttpContext, string, Task<IEnumerable<Claim>>>)null!);
        _ = optionsMock.SetupGet(o => o.Logger).Returns(loggerMock.Object);

        var scheme = new AuthenticationScheme("TestScheme", null, typeof(BasicAuthHandler));
        var user = "testuser";

        // Act
        var ticket = await IAuthHandler.GetAuthenticationTicketAsync(context, user, optionsMock.Object, scheme);

        // Assert
        Assert.NotNull(ticket);
        Assert.Equal("TestScheme", ticket.AuthenticationScheme);
        var nameClaim = ticket.Principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        Assert.NotNull(nameClaim);
        Assert.Equal(user, nameClaim.Value);
    }

    [Fact]
    public async Task ValidatePowerShellAsync_ReturnsFalse_WhenCredentialsAreNull()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items["PS_INSTANCE"] = PowerShell.Create();
        var loggerMock = new Mock<ILogger>();
        var result = await IAuthHandler.ValidatePowerShellAsync("return $true", context, [], loggerMock.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidatePowerShellAsync_ReturnsFalse_WhenCodeIsNull()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items["PS_INSTANCE"] = PowerShell.Create();
        var loggerMock = new Mock<ILogger>();
        var credentials = new Dictionary<string, string> { { "username", "u" }, { "password", "p" } };

        var result = await IAuthHandler.ValidatePowerShellAsync(null, context, credentials, loggerMock.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IssueClaimsPowerShellAsync_ReturnsEmpty_WhenIdentityIsNull()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items["PS_INSTANCE"] = PowerShell.Create();
        var loggerMock = new Mock<ILogger>();

        var claims = await IAuthHandler.IssueClaimsPowerShellAsync("return $true", context, "", loggerMock.Object);

        Assert.Empty(claims);
    }

    [Fact]
    public void BuildCsIssueClaims_ReturnsFunc()
    {
        // Arrange
        foreach (var key in SharedStateStore.KeySnapshot())
        {
            _ = SharedStateStore.Set(key, null);
        }
        var settings = new AuthenticationCodeSettings
        {
            Code = "return (System.Collections.Generic.IEnumerable<System.Security.Claims.Claim>) new System.Collections.Generic.List<System.Security.Claims.Claim>() { new System.Security.Claims.Claim(\"role\", \"admin\") };",
            CSharpVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest,
            ExtraImports = ["System", "System.Linq", "System.Collections.Generic", "System.Security.Claims", "Kestrun", "Microsoft.AspNetCore.Http"]
        };
        var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();
        var host = new Kestrun.Hosting.KestrunHost("Tests", logger);

        // Act
        var func = IAuthHandler.BuildCsIssueClaims(host, settings);

        // Assert
        Assert.NotNull(func);
    }

    [Fact]
    public void BuildVBNetIssueClaims_ReturnsFunc()
    {
        // Arrange
        var settings = new AuthenticationCodeSettings
        {
            Code = "Return New System.Collections.Generic.List(Of System.Security.Claims.Claim)() From { New System.Security.Claims.Claim(\"role\", \"admin\") }",
            VisualBasicVersion = Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.Latest
        };
        var loggerMock = new Mock<ILogger>();
        var host = new Kestrun.Hosting.KestrunHost("Tests", loggerMock.Object);

        // Act
        var func = IAuthHandler.BuildVBNetIssueClaims(host, settings);

        // Assert
        Assert.NotNull(func);
    }

    [Fact]
    public void TryToClaim_ReturnsTrue_ForClaimInstance()
    {
        // Arrange
        var method = typeof(IAuthHandler).GetMethod("TryToClaim", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var input = new Claim("role", "admin");
        var args = new object?[] { input, null };

        // Act
        var ok = (bool)method!.Invoke(null, args)!;
        var claim = args[1] as Claim;

        // Assert
        Assert.True(ok);
        Assert.NotNull(claim);
        Assert.Equal("role", claim!.Type);
        Assert.Equal("admin", claim.Value);
    }

    [Fact]
    public void TryToClaim_ReturnsTrue_ForDictionaryWithTypeAndValue()
    {
        // Arrange
        var method = typeof(IAuthHandler).GetMethod("TryToClaim", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        IDictionary dict = new Dictionary<string, object?>
        {
            { "Type", ClaimTypes.Email },
            { "Value", "user@example.com" }
        };

        var args = new object?[] { dict, null };

        // Act
        var ok = (bool)method!.Invoke(null, args)!;
        var claim = args[1] as Claim;

        // Assert
        Assert.True(ok);
        Assert.NotNull(claim);
        Assert.Equal(ClaimTypes.Email, claim!.Type);
        Assert.Equal("user@example.com", claim.Value);
    }

    [Fact]
    public void TryToClaim_ReturnsTrue_ForColonSeparatedString()
    {
        // Arrange
        var method = typeof(IAuthHandler).GetMethod("TryToClaim", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var input = "scope:read";
        var args = new object?[] { input, null };

        // Act
        var ok = (bool)method!.Invoke(null, args)!;
        var claim = args[1] as Claim;

        // Assert
        Assert.True(ok);
        Assert.NotNull(claim);
        Assert.Equal("scope", claim!.Type);
        Assert.Equal("read", claim.Value);
    }

    [Fact]
    public void TryToClaim_ReturnsFalse_ForUnsupportedInput()
    {
        // Arrange
        var method = typeof(IAuthHandler).GetMethod("TryToClaim", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var args = new object?[] { 42, null };

        // Act
        var ok = (bool)method!.Invoke(null, args)!;

        // Assert
        Assert.False(ok);
        Assert.Null(args[1]);
    }

    [Fact]
    public void GetPowerShell_Throws_WhenMissingFromContext()
    {
        // Arrange
        var getPs = typeof(IAuthHandler).GetMethod("GetPowerShell", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(getPs);

        var context = new DefaultHttpContext();

        // Act + Assert
        var ex = Assert.Throws<TargetInvocationException>(() => getPs!.Invoke(null, [context]));
        _ = Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void GetPowerShell_ReturnsInstance_WhenPresentInContext()
    {
        // Arrange
        var getPs = typeof(IAuthHandler).GetMethod("GetPowerShell", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(getPs);

        var context = new DefaultHttpContext();
        using var runspace = System.Management.Automation.Runspaces.RunspaceFactory.CreateRunspace();
        runspace.Open();
        using var ps = PowerShell.Create();
        ps.Runspace = runspace;
        context.Items["PS_INSTANCE"] = ps;

        // Act
        var result = getPs!.Invoke(null, [context]);

        // Assert
        Assert.NotNull(result);
        _ = Assert.IsType<PowerShell>(result);
    }
}

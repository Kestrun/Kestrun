using Kestrun.Launcher;
using Xunit;

namespace Kestrun.Launcher.Tests;

public class ArgsTests
{
    [Fact]
    public void Parse_Help_SetsHelpFlag()
    {
        // Arrange
        var args = new[] { "--help" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.True(parsed.Help);
    }

    [Fact]
    public void Parse_HelpShortForm_SetsHelpFlag()
    {
        // Arrange
        var args = new[] { "-h" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.True(parsed.Help);
    }

    [Fact]
    public void Parse_Version_SetsVersionFlag()
    {
        // Arrange
        var args = new[] { "--version" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.True(parsed.Version);
    }

    [Fact]
    public void Parse_VersionShortForm_SetsVersionFlag()
    {
        // Arrange
        var args = new[] { "-v" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.True(parsed.Version);
    }

    [Fact]
    public void Parse_RunCommand_SetsCommandAndPath()
    {
        // Arrange
        var args = new[] { "run", "/path/to/app" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal(LauncherCommand.Run, parsed.Command);
        Assert.Equal("/path/to/app", parsed.AppPath);
    }

    [Fact]
    public void Parse_InstallCommand_SetsCommandAndPath()
    {
        // Arrange
        var args = new[] { "install", "/path/to/app" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal(LauncherCommand.Install, parsed.Command);
        Assert.Equal("/path/to/app", parsed.AppPath);
    }

    [Fact]
    public void Parse_UninstallCommand_SetsCommand()
    {
        // Arrange
        var args = new[] { "uninstall" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal(LauncherCommand.Uninstall, parsed.Command);
    }

    [Fact]
    public void Parse_StartCommand_SetsCommand()
    {
        // Arrange
        var args = new[] { "start" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal(LauncherCommand.Start, parsed.Command);
    }

    [Fact]
    public void Parse_StopCommand_SetsCommand()
    {
        // Arrange
        var args = new[] { "stop" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal(LauncherCommand.Stop, parsed.Command);
    }

    [Fact]
    public void Parse_ServiceName_SetsServiceName()
    {
        // Arrange
        var args = new[] { "start", "--service-name", "MyService" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal("MyService", parsed.ServiceName);
    }

    [Fact]
    public void Parse_ServiceNameShortForm_SetsServiceName()
    {
        // Arrange
        var args = new[] { "start", "-n", "MyService" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal("MyService", parsed.ServiceName);
    }

    [Fact]
    public void Parse_PathOption_SetsAppPath()
    {
        // Arrange
        var args = new[] { "run", "--path", "/path/to/app" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal("/path/to/app", parsed.AppPath);
    }

    [Fact]
    public void Parse_PathOptionShortForm_SetsAppPath()
    {
        // Arrange
        var args = new[] { "run", "-p", "/path/to/app" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal("/path/to/app", parsed.AppPath);
    }

    [Fact]
    public void Parse_PositionalPath_SetsAppPath()
    {
        // Arrange
        var args = new[] { "/path/to/app" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal("/path/to/app", parsed.AppPath);
    }

    [Fact]
    public void Validate_HelpCommand_ReturnsTrue()
    {
        // Arrange
        var parsed = new Args { Help = true };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_VersionCommand_ReturnsTrue()
    {
        // Arrange
        var parsed = new Args { Version = true };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_InstallWithoutServiceName_ReturnsFalse()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var parsed = new Args 
        { 
            Command = LauncherCommand.Install,
            AppPath = tempDir
        };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("service name", error);
    }

    [Fact]
    public void Validate_UninstallWithoutServiceName_ReturnsFalse()
    {
        // Arrange
        var parsed = new Args { Command = LauncherCommand.Uninstall };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("service name", error);
    }

    [Fact]
    public void Validate_RunWithoutPath_ReturnsFalse()
    {
        // Arrange
        var parsed = new Args { Command = LauncherCommand.Run };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("app path", error);
    }

    [Fact]
    public void Validate_RunWithNonExistentPath_ReturnsFalse()
    {
        // Arrange
        var parsed = new Args 
        { 
            Command = LauncherCommand.Run,
            AppPath = "/non/existent/path"
        };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void Validate_RunWithValidPath_ReturnsTrue()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var parsed = new Args 
        { 
            Command = LauncherCommand.Run,
            AppPath = tempDir
        };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_InstallWithValidPathAndServiceName_ReturnsTrue()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var parsed = new Args 
        { 
            Command = LauncherCommand.Install,
            AppPath = tempDir,
            ServiceName = "TestService"
        };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void Parse_KestrunModulePath_SetsKestrunModulePath()
    {
        // Arrange
        var args = new[] { "run", "/path/to/app", "--kestrun-module", "/path/to/module" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal("/path/to/module", parsed.KestrunModulePath);
    }

    [Fact]
    public void Parse_KestrunModulePathShortForm_SetsKestrunModulePath()
    {
        // Arrange
        var args = new[] { "run", "/path/to/app", "-k", "/path/to/module" };

        // Act
        var parsed = Args.Parse(args);

        // Assert
        Assert.Equal("/path/to/module", parsed.KestrunModulePath);
    }

    [Fact]
    public void Validate_InvalidKestrunModulePath_ReturnsFalse()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var parsed = new Args 
        { 
            Command = LauncherCommand.Run,
            AppPath = tempDir,
            KestrunModulePath = "/non/existent/module.psd1"
        };

        // Act
        var isValid = parsed.Validate(out var error);

        // Assert
        Assert.False(isValid);
        Assert.Contains("Kestrun module path does not exist", error);
    }
}

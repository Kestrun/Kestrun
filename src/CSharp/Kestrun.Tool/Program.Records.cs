using System.Text.RegularExpressions;

namespace Kestrun.Tool;

internal static partial class Program
{
    private const string ModuleManifestFileName = "Kestrun.psd1";
    private const string ServiceDescriptorFileName = "Service.psd1";
    private const string ModuleName = "Kestrun";
    private const string DefaultScriptFileName = "server.ps1";
    private const string ProductName = "kestrun";
    private const string ServiceDeploymentProductFolderName = "Kestrun";
    private const string ServiceDeploymentServicesFolderName = "services";
    private const string ServiceBundleRuntimeDirectoryName = "runtime";
    private const string ServiceBundleModulesDirectoryName = "Modules";
    private const string ServiceBundleScriptDirectoryName = "script";
    private const string WindowsServiceRuntimeBinaryName = "kestrun.exe";
    private const string UnixServiceRuntimeBinaryName = "kestrun";
    private const string ModuleVersionOption = "--version";
    private const string ModuleScopeOption = "--scope";
    private const string ModuleForceOption = "--force";
    private const string ModuleScopeLocalValue = "local";
    private const string ModuleScopeGlobalValue = "global";
    private const string NoCheckOption = "--nocheck";
    private const string NoCheckAliasOption = "--no-check";
    private const string PowerShellGalleryApiBaseUri = "https://www.powershellgallery.com/api/v2";
    private static readonly Regex ModuleVersionPatternRegex = ModuleVersionRegex();
    private static readonly Regex ModulePrereleasePatternRegex = ModulePrereleaseRegex();
    private static readonly HttpClient GalleryHttpClient = CreateGalleryHttpClient();
    private static readonly HttpClient ServiceContentRootHttpClient = CreateServiceContentRootHttpClient();
    private static readonly string[] ServiceBundleModuleExclusionPatterns =
    [
        "lib/runtimes/*",
        "lib/net8.0/*",
        "lib/Microsoft.CodeAnalysis/4*/*",
    ];
    private enum CommandMode
    {
        Run,
        ModuleInstall,
        ModuleUpdate,
        ModuleRemove,
        ModuleInfo,
        ServiceInstall,
        ServiceRemove,
        ServiceStart,
        ServiceStop,
        ServiceQuery,
    }

    private enum ModuleCommandAction
    {
        Install,
        Update,
        Remove,
    }

    private enum ModuleStorageScope
    {
        Local,
        Global,
    }

    private sealed record ParsedCommand(
        CommandMode Mode,
        string ScriptPath,
        bool ScriptPathProvided,
        string[] ScriptArguments,
        string? KestrunFolder,
        string? KestrunManifestPath,
        string? ServiceName,
        bool ServiceNameProvided,
        string? ServiceLogPath,
        string? ServiceUser,
        string? ServicePassword,
        string? ModuleVersion,
        ModuleStorageScope ModuleScope,
        bool ModuleForce,
        string? ServiceContentRoot,
        string? ServiceDeploymentRoot,
        string? ServiceContentRootChecksum,
        string? ServiceContentRootChecksumAlgorithm,
        string? ServiceContentRootBearerToken,
        bool ServiceContentRootIgnoreCertificate,
        string[] ServiceContentRootHeaders);

    private sealed record ServiceRegisterOptions(
        string ServiceName,
        string ServiceHostExecutablePath,
        string RunnerExecutablePath,
        string ScriptPath,
        string ModuleManifestPath,
        string[] ScriptArguments,
        string? ServiceLogPath,
        string? ServiceUser,
        string? ServicePassword);

    private sealed record GlobalOptions(
        string[] CommandArgs,
        bool SkipGalleryCheck);

    private sealed record InstalledModuleRecord(
        string Version,
        string ManifestPath);

    private sealed record ServiceBundleLayout(
        string RootPath,
        string RuntimeExecutablePath,
        string ServiceHostExecutablePath,
        string ScriptPath,
        string ModuleManifestPath);

    private sealed record ResolvedServiceScriptSource(
        string FullScriptPath,
        string? FullContentRoot,
        string RelativeScriptPath,
        string? TemporaryContentRootPath,
        string? DescriptorServiceName,
        string? DescriptorServiceDescription,
        string? DescriptorServiceVersion,
        string? DescriptorServiceLogPath);

    private sealed record ServiceInstallDescriptor(
        string Name,
        string Description,
        string Version,
        string? ScriptPath,
        string? ServiceLogPath);

    [GeneratedRegex("--service-log-path\\s+(\\\"(?<quoted>[^\\\"]+)\\\"|(?<plain>\\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ServiceLogPathRegex();
    [GeneratedRegex("^\\s*ModuleVersion\\s*=\\s*['\\\"](?<value>[^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ModuleVersionRegex();
    [GeneratedRegex("^\\s*Prerelease\\s*=\\s*['\\\"](?<value>[^'\\\"]+)['\\\"]", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ModulePrereleaseRegex();
}

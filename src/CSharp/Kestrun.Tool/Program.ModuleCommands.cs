namespace Kestrun.Tool;

internal static partial class Program
{
    /// <summary>
    /// Executes a module management command.
    /// </summary>
    /// <param name="command">Parsed module command.</param>
    /// <returns>Process exit code.</returns>
    private static int ManageModuleCommand(ParsedCommand command)
    {
        return command.Mode switch
        {
            CommandMode.ModuleInstall => ManageModuleFromGallery(ModuleCommandAction.Install, command.ModuleVersion, command.ModuleScope, command.ModuleForce),
            CommandMode.ModuleUpdate => ManageModuleFromGallery(ModuleCommandAction.Update, command.ModuleVersion, command.ModuleScope, command.ModuleForce),
            CommandMode.ModuleRemove => ManageModuleFromGallery(ModuleCommandAction.Remove, null, command.ModuleScope, force: false),
            CommandMode.ModuleInfo => PrintModuleInfo(command.ModuleScope),
            _ => throw new InvalidOperationException($"Unsupported module mode: {command.Mode}"),
        };
    }

    /// <summary>
    /// Prints module installation details for local and gallery versions.
    /// </summary>
    /// <returns>Process exit code.</returns>
    private static int PrintModuleInfo(ModuleStorageScope scope)
    {
        var modulePath = GetPowerShellModulePath(scope);
        var moduleRoot = Path.Combine(modulePath, ModuleName);
        var records = GetInstalledModuleRecords(moduleRoot);
        var latestInstalledVersionText = records.Count > 0 ? records[0].Version : null;

        Console.WriteLine($"Module name: {ModuleName}");
        Console.WriteLine($"Selected module scope: {GetScopeToken(scope)}");
        Console.WriteLine($"Module path root: {modulePath}");

        if (records.Count == 0)
        {
            Console.WriteLine("Installed versions: none");
        }
        else
        {
            Console.WriteLine("Installed versions:");
            foreach (var record in records)
            {
                Console.WriteLine($"  - {record.Version} ({Path.GetDirectoryName(record.ManifestPath)})");
            }
        }

        if (TryGetLatestGalleryVersionString(out var galleryVersion, out _))
        {
            Console.WriteLine($"Latest PowerShell Gallery version: {galleryVersion}");

            if (!string.IsNullOrWhiteSpace(latestInstalledVersionText)
                && CompareModuleVersionValues(galleryVersion, latestInstalledVersionText) > 0)
            {
                Console.WriteLine($"Update available: run '{ProductName} module update'.");
            }
        }
        else
        {
            Console.WriteLine("Latest PowerShell Gallery version: unavailable");
        }

        return 0;
    }

    /// <summary>
    /// Installs, updates, or removes the Kestrun module in the current-user module path.
    /// </summary>
    /// <param name="action">Module action to perform.</param>
    /// <param name="version">Optional specific module version.</param>
    /// <param name="scope">Module installation scope.</param>
    /// <param name="force">When true, update overwrites an existing target version folder.</param>
    /// <returns>Process exit code.</returns>
    private static int ManageModuleFromGallery(ModuleCommandAction action, string? version, ModuleStorageScope scope, bool force)
    {
        var modulePath = GetPowerShellModulePath(scope);
        var moduleRoot = Path.Combine(modulePath, ModuleName);

        if (action == ModuleCommandAction.Remove)
        {
            if (!TryRemoveInstalledModule(moduleRoot, !Console.IsOutputRedirected, out var removeErrorText))
            {
                Console.Error.WriteLine($"Failed to remove '{ModuleName}' module.");
                if (!string.IsNullOrWhiteSpace(removeErrorText))
                {
                    Console.Error.WriteLine(removeErrorText);
                }

                return 1;
            }

            Console.WriteLine($"{ModuleName} module removed from {GetScopeToken(scope)} module path.");
            Console.WriteLine($"Module root: {moduleRoot}");
            return 0;
        }

        if (action == ModuleCommandAction.Install
            && !TryValidateInstallAction(moduleRoot, GetScopeToken(scope), out var installValidationError))
        {
            Console.Error.WriteLine(installValidationError);
            return 1;
        }

        if (!TryInstallOrUpdateModuleFromGallery(action, version, moduleRoot, !Console.IsOutputRedirected, force, out var installedVersion, out var installedManifestPath, out var errorText))
        {
            Console.Error.WriteLine($"Failed to {action.ToString().ToLowerInvariant()} '{ModuleName}' module.");
            if (!string.IsNullOrWhiteSpace(errorText))
            {
                Console.Error.WriteLine(errorText);
            }

            return 1;
        }

        var installedPath = Path.GetDirectoryName(installedManifestPath) ?? Path.Combine(moduleRoot, installedVersion);
        var versionSuffix = string.IsNullOrWhiteSpace(installedVersion)
            ? string.Empty
            : $" (version {installedVersion})";

        if (action == ModuleCommandAction.Install)
        {
            Console.WriteLine($"{ModuleName} module installed{versionSuffix} to {GetScopeToken(scope)} scope.");
        }
        else
        {
            Console.WriteLine($"{ModuleName} module updated{versionSuffix} in {GetScopeToken(scope)} scope.");
        }

        Console.WriteLine($"Module path: {installedPath}");
        return 0;
    }

    /// <summary>
    /// Downloads a module package from PowerShell Gallery and installs it into the user module path.
    /// </summary>
    /// <param name="action">Module action being executed.</param>
    /// <param name="requestedVersion">Optional requested package version.</param>
    /// <param name="moduleRoot">Root folder for module versions.</param>
    /// <param name="installedVersion">Installed module version.</param>
    /// <param name="installedManifestPath">Installed manifest path.</param>
    /// <param name="errorText">Error details when installation fails.</param>
    /// <returns>True when install/update succeeds.</returns>
    private static bool TryInstallOrUpdateModuleFromGallery(
        ModuleCommandAction action,
        string? requestedVersion,
        string moduleRoot,
        bool showProgress,
        bool force,
        out string installedVersion,
        out string installedManifestPath,
        out string errorText)
    {
        installedVersion = string.Empty;
        installedManifestPath = string.Empty;
        if (!TryDownloadModulePackage(requestedVersion, showProgress, out var packageBytes, out var packageVersion, out errorText))
        {
            return false;
        }

        if (action == ModuleCommandAction.Update
            && !TryValidateUpdateAction(moduleRoot, packageVersion, force, out errorText))
        {
            return false;
        }

        if (!TryExtractModulePackage(packageBytes, packageVersion, moduleRoot, showProgress, force, out installedManifestPath, out errorText))
        {
            return false;
        }

        installedVersion = packageVersion;
        return true;
    }
}

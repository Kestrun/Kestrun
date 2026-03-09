using System.IO.Compression;
using System.Net;

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

    /// <summary>
    /// Downloads the Kestrun nupkg package from PowerShell Gallery.
    /// </summary>
    /// <param name="requestedVersion">Optional requested package version.</param>
    /// <param name="packageBytes">Downloaded package payload.</param>
    /// <param name="packageVersion">Resolved package version from nuspec metadata.</param>
    /// <param name="errorText">Error details when download fails.</param>
    /// <returns>True when the package download succeeds.</returns>
    private static bool TryDownloadModulePackage(
        string? requestedVersion,
        bool showProgress,
        out byte[] packageBytes,
        out string packageVersion,
        out string errorText)
    {
        packageBytes = [];
        packageVersion = string.Empty;
        errorText = string.Empty;

        try
        {
            var normalizedVersion = NormalizeRequestedModuleVersion(requestedVersion);
            var packageUrl = BuildGalleryPackageUrl(normalizedVersion);

            using var request = new HttpRequestMessage(HttpMethod.Get, packageUrl);
            using var response = GalleryHttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
            if (!TryHandlePackageDownloadResponseStatus(
                    response,
                    normalizedVersion,
                    showProgress,
                    out packageBytes,
                    out packageVersion,
                    out errorText))
            {
                return false;
            }

            if (!TryDownloadPackagePayload(response, showProgress, out packageBytes, out errorText))
            {
                return false;
            }
            // Attempt to resolve package version from nuspec metadata, with fallback to normalized version input when resolution fails.
            if (!TryResolveDownloadedPackageVersion(packageBytes, normalizedVersion, out packageVersion, out errorText))
            {
                return false;
            }
            // Package download and version resolution succeeded.
            return true;
        }
        catch (Exception ex)
        {
            errorText = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Normalizes optional module version input used for gallery package requests.
    /// </summary>
    /// <param name="requestedVersion">Requested version text.</param>
    /// <returns>Trimmed version text or null when not specified.</returns>
    private static string? NormalizeRequestedModuleVersion(string? requestedVersion)
        => string.IsNullOrWhiteSpace(requestedVersion) ? null : requestedVersion.Trim();

    /// <summary>
    /// Builds the PowerShell Gallery package URL for a module and optional version.
    /// </summary>
    /// <param name="normalizedVersion">Optional normalized version.</param>
    /// <returns>Gallery package URL.</returns>
    private static string BuildGalleryPackageUrl(string? normalizedVersion)
    {
        return string.IsNullOrWhiteSpace(normalizedVersion)
            ? $"{PowerShellGalleryApiBaseUri}/package/{Uri.EscapeDataString(ModuleName)}"
            : $"{PowerShellGalleryApiBaseUri}/package/{Uri.EscapeDataString(ModuleName)}/{Uri.EscapeDataString(normalizedVersion)}";
    }

    /// <summary>
    /// Validates HTTP status and handles not-found fallback for module package downloads.
    /// </summary>
    /// <param name="response">HTTP response from gallery.</param>
    /// <param name="normalizedVersion">Optional normalized version.</param>
    /// <param name="showProgress">True to show download progress.</param>
    /// <param name="packageBytes">Downloaded package bytes when fallback succeeds.</param>
    /// <param name="packageVersion">Downloaded package version when fallback succeeds.</param>
    /// <param name="errorText">Error details when status handling fails.</param>
    /// <returns>True when status is acceptable and caller should continue payload processing.</returns>
    private static bool TryHandlePackageDownloadResponseStatus(
        HttpResponseMessage response,
        string? normalizedVersion,
        bool showProgress,
        out byte[] packageBytes,
        out string packageVersion,
        out string errorText)
    {
        packageBytes = [];
        packageVersion = string.Empty;
        errorText = string.Empty;

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            if (string.IsNullOrWhiteSpace(normalizedVersion)
                && TryGetLatestGalleryVersionString(out var latestVersion, out _)
                && !string.IsNullOrWhiteSpace(latestVersion))
            {
                return TryDownloadModulePackage(latestVersion, showProgress, out packageBytes, out packageVersion, out errorText);
            }

            errorText = string.IsNullOrWhiteSpace(normalizedVersion)
                ? $"Module '{ModuleName}' was not found on PowerShell Gallery."
                : $"Module '{ModuleName}' version '{normalizedVersion}' was not found on PowerShell Gallery.";
            return false;
        }

        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? "Unknown error"
            : response.ReasonPhrase;
        errorText = $"PowerShell Gallery request failed with HTTP {(int)response.StatusCode} ({reason}).";
        return false;
    }

    /// <summary>
    /// Downloads package bytes from a successful gallery response stream.
    /// </summary>
    /// <param name="response">Successful HTTP response.</param>
    /// <param name="showProgress">True to display progress output.</param>
    /// <param name="packageBytes">Downloaded package bytes.</param>
    /// <param name="errorText">Error details when payload is empty.</param>
    /// <returns>True when payload download succeeds with non-empty content.</returns>
    private static bool TryDownloadPackagePayload(HttpResponseMessage response, bool showProgress, out byte[] packageBytes, out string errorText)
    {
        errorText = string.Empty;

        var contentLength = response.Content.Headers.ContentLength;
        using var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var packageStream = contentLength.HasValue && contentLength.Value > 0 && contentLength.Value <= int.MaxValue
            ? new MemoryStream((int)contentLength.Value)
            : new MemoryStream();

        using var downloadProgress = showProgress
            ? new ConsoleProgressBar("Downloading package", contentLength, FormatByteProgressDetail)
            : null;

        CopyStreamWithProgress(responseStream, packageStream, downloadProgress);
        packageBytes = packageStream.ToArray();
        if (packageBytes.Length > 0)
        {
            return true;
        }

        errorText = "Downloaded package was empty.";
        return false;
    }

    /// <summary>
    /// Resolves module version from package metadata, with version-input fallback normalization.
    /// </summary>
    /// <param name="packageBytes">Downloaded package bytes.</param>
    /// <param name="normalizedVersion">Optional normalized requested version.</param>
    /// <param name="packageVersion">Resolved package version.</param>
    /// <param name="errorText">Error details when version resolution fails.</param>
    /// <returns>True when package version is resolved.</returns>
    private static bool TryResolveDownloadedPackageVersion(
        byte[] packageBytes,
        string? normalizedVersion,
        out string packageVersion,
        out string errorText)
    {
        errorText = string.Empty;

        if (TryReadPackageVersion(packageBytes, out packageVersion))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(normalizedVersion))
        {
            if (TryNormalizeModuleVersion(normalizedVersion, out packageVersion))
            {
                return true;
            }

            errorText = $"Unable to normalize package version '{normalizedVersion}' for module folder naming.";
            return false;
        }

        packageVersion = string.Empty;
        errorText = "Unable to determine package version from downloaded metadata.";
        return false;
    }

    /// <summary>
    /// Extracts a module package payload and installs it under the versioned module directory.
    /// </summary>
    /// <param name="packageBytes">Downloaded package bytes.</param>
    /// <param name="packageVersion">Package version used for destination folder naming.</param>
    /// <param name="moduleRoot">Root directory for module versions.</param>
    /// <param name="installedManifestPath">Installed module manifest path.</param>
    /// <param name="errorText">Error details when extraction fails.</param>
    /// <returns>True when package extraction and install succeed.</returns>
    private static bool TryExtractModulePackage(
        byte[] packageBytes,
        string packageVersion,
        string moduleRoot,
        bool showProgress,
        bool allowOverwrite,
        out string installedManifestPath,
        out string errorText)
    {
        installedManifestPath = string.Empty;
        errorText = string.Empty;

        if (packageVersion.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            errorText = $"Invalid package version '{packageVersion}' for filesystem install path.";
            return false;
        }

        var stagingPath = Path.Combine(Path.GetTempPath(), $"{ProductName}-module-{Guid.NewGuid():N}");
        var comparisonType = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        try
        {
            _ = Directory.CreateDirectory(stagingPath);

            using var packageStream = new MemoryStream(packageBytes, writable: false);
            using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);

            if (!TryCollectModulePayloadEntries(archive, out var payloadEntries, out errorText))
            {
                return false;
            }

            var shouldStripModulePrefix = ShouldStripModulePrefix(payloadEntries);
            if (!TryExtractPayloadEntriesToStaging(payloadEntries, stagingPath, comparisonType, shouldStripModulePrefix, showProgress, out errorText))
            {
                return false;
            }

            if (!TryResolveExtractedManifestPath(stagingPath, out var manifestPath, out errorText))
            {
                return false;
            }

            if (!TryInstallExtractedModule(manifestPath, moduleRoot, packageVersion, showProgress, allowOverwrite, out installedManifestPath, out errorText))
            {
                return false;
            }

            return File.Exists(installedManifestPath);
        }
        catch (Exception ex)
        {
            errorText = ex.Message;
            return false;
        }
        finally
        {
            TryDeleteDirectoryQuietly(stagingPath);
        }
    }

    /// <summary>
    /// Collects package entries that belong to the module payload.
    /// </summary>
    /// <param name="archive">Opened package archive.</param>
    /// <param name="payloadEntries">Collected payload entries and relative paths.</param>
    /// <param name="errorText">Error details when no payload entries are found.</param>
    /// <returns>True when payload entries are discovered.</returns>
    private static bool TryCollectModulePayloadEntries(
        ZipArchive archive,
        out List<(ZipArchiveEntry Entry, string RelativePath)> payloadEntries,
        out string errorText)
    {
        payloadEntries = [];
        errorText = string.Empty;

        foreach (var entry in archive.Entries)
        {
            if (TryGetPackagePayloadPath(entry.FullName, out var relativePath))
            {
                payloadEntries.Add((entry, relativePath));
            }
        }

        if (payloadEntries.Count > 0)
        {
            return true;
        }

        errorText = "Package did not contain any module payload files.";
        return false;
    }

    /// <summary>
    /// Determines whether all payload entries share a top-level module-name folder prefix.
    /// </summary>
    /// <param name="payloadEntries">Collected payload entries.</param>
    /// <returns>True when module prefix should be stripped while extracting.</returns>
    private static bool ShouldStripModulePrefix(IReadOnlyList<(ZipArchiveEntry Entry, string RelativePath)> payloadEntries)
    {
        return payloadEntries.All(static payloadEntry =>
        {
            var separatorIndex = payloadEntry.RelativePath.IndexOf('/');
            return separatorIndex > 0
                && string.Equals(payloadEntry.RelativePath[..separatorIndex], ModuleName, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// Extracts payload entries into staging while enforcing path traversal protection.
    /// </summary>
    /// <param name="payloadEntries">Collected payload entries.</param>
    /// <param name="stagingPath">Extraction staging path.</param>
    /// <param name="comparisonType">Path comparison mode for security checks.</param>
    /// <param name="shouldStripModulePrefix">True when module-name prefix should be removed.</param>
    /// <param name="showProgress">True to show extraction progress.</param>
    /// <param name="errorText">Error details when extraction fails.</param>
    /// <returns>True when extraction succeeds.</returns>
    private static bool TryExtractPayloadEntriesToStaging(
        IReadOnlyList<(ZipArchiveEntry Entry, string RelativePath)> payloadEntries,
        string stagingPath,
        StringComparison comparisonType,
        bool shouldStripModulePrefix,
        bool showProgress,
        out string errorText)
    {
        errorText = string.Empty;

        using var extractProgress = showProgress
            ? new ConsoleProgressBar("Extracting package", payloadEntries.Count, FormatFileProgressDetail)
            : null;
        var extractedEntryCount = 0;
        extractProgress?.Report(0);

        var fullStagingPath = Path.GetFullPath(stagingPath);
        var fullStagingPathWithSeparator = Path.EndsInDirectorySeparator(fullStagingPath)
            ? fullStagingPath
            : fullStagingPath + Path.DirectorySeparatorChar;

        foreach (var payloadEntry in payloadEntries)
        {
            var relativePath = NormalizePayloadRelativePath(payloadEntry.RelativePath, shouldStripModulePrefix);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            if (!TryResolveSafeStagingDestination(
                    stagingPath,
                    fullStagingPathWithSeparator,
                    payloadEntry.Entry.FullName,
                    relativePath,
                    comparisonType,
                    out var destinationPath,
                    out errorText))
            {
                return false;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                _ = Directory.CreateDirectory(destinationDirectory);
            }

            payloadEntry.Entry.ExtractToFile(destinationPath, overwrite: true);
            extractedEntryCount++;
            extractProgress?.Report(extractedEntryCount);
        }

        extractProgress?.Complete(extractedEntryCount);
        return true;
    }

    /// <summary>
    /// Normalizes extracted payload relative paths and optionally strips module-name prefix.
    /// </summary>
    /// <param name="relativePath">Raw payload relative path.</param>
    /// <param name="shouldStripModulePrefix">True when module prefix should be stripped.</param>
    /// <returns>Normalized relative path for extraction.</returns>
    private static string NormalizePayloadRelativePath(string relativePath, bool shouldStripModulePrefix)
    {
        if (!shouldStripModulePrefix)
        {
            return relativePath;
        }

        var separatorIndex = relativePath.IndexOf('/');
        return separatorIndex >= 0
            ? relativePath[(separatorIndex + 1)..]
            : relativePath;
    }

    /// <summary>
    /// Resolves and validates a destination path inside staging for one payload entry.
    /// </summary>
    /// <param name="stagingPath">Staging root path.</param>
    /// <param name="fullStagingPathWithSeparator">Normalized staging root with trailing separator.</param>
    /// <param name="entryFullName">Original package entry path.</param>
    /// <param name="relativePath">Normalized relative payload path.</param>
    /// <param name="comparisonType">Path comparison mode.</param>
    /// <param name="destinationPath">Resolved destination path.</param>
    /// <param name="errorText">Error details when traversal is detected.</param>
    /// <returns>True when destination path is safe for extraction.</returns>
    private static bool TryResolveSafeStagingDestination(
        string stagingPath,
        string fullStagingPathWithSeparator,
        string entryFullName,
        string relativePath,
        StringComparison comparisonType,
        out string destinationPath,
        out string errorText)
    {
        errorText = string.Empty;
        destinationPath = Path.GetFullPath(Path.Combine(stagingPath, relativePath));
        if (destinationPath.StartsWith(fullStagingPathWithSeparator, comparisonType))
        {
            return true;
        }

        errorText = $"Package entry '{entryFullName}' resolves outside staging directory.";
        return false;
    }

    /// <summary>
    /// Resolves the extracted module manifest path from staging.
    /// </summary>
    /// <param name="stagingPath">Extraction staging path.</param>
    /// <param name="manifestPath">Resolved manifest path.</param>
    /// <param name="errorText">Error details when manifest is missing.</param>
    /// <returns>True when manifest path is resolved.</returns>
    private static bool TryResolveExtractedManifestPath(string stagingPath, out string manifestPath, out string errorText)
    {
        errorText = string.Empty;
        manifestPath = Directory.EnumerateFiles(stagingPath, ModuleManifestFileName, SearchOption.AllDirectories)
            .FirstOrDefault(static path => path is not null) ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            return true;
        }

        errorText = $"Package payload did not contain '{ModuleManifestFileName}'.";
        return false;
    }

    /// <summary>
    /// Installs extracted module files into the versioned module destination folder.
    /// </summary>
    /// <param name="manifestPath">Resolved staging manifest path.</param>
    /// <param name="moduleRoot">Root module folder.</param>
    /// <param name="packageVersion">Package version folder name.</param>
    /// <param name="showProgress">True to show copy progress.</param>
    /// <param name="allowOverwrite">True when existing destination can be replaced.</param>
    /// <param name="installedManifestPath">Installed manifest destination path.</param>
    /// <param name="errorText">Error details when destination cannot be prepared.</param>
    /// <returns>True when install copy succeeds.</returns>
    private static bool TryInstallExtractedModule(
        string manifestPath,
        string moduleRoot,
        string packageVersion,
        bool showProgress,
        bool allowOverwrite,
        out string installedManifestPath,
        out string errorText)
    {
        installedManifestPath = string.Empty;
        errorText = string.Empty;

        var sourceModuleDirectory = Path.GetDirectoryName(manifestPath)!;
        var destinationModuleDirectory = Path.Combine(moduleRoot, packageVersion);

        if (Directory.Exists(destinationModuleDirectory))
        {
            if (!allowOverwrite)
            {
                errorText = $"Target module version folder already exists: {destinationModuleDirectory}";
                return false;
            }

            Directory.Delete(destinationModuleDirectory, recursive: true);
        }

        CopyDirectoryContents(sourceModuleDirectory, destinationModuleDirectory, showProgress);
        installedManifestPath = Path.Combine(destinationModuleDirectory, ModuleManifestFileName);
        return true;
    }

    /// <summary>
    /// Best-effort directory cleanup used for temporary extraction staging folders.
    /// </summary>
    /// <param name="directoryPath">Directory path to delete.</param>
    private static void TryDeleteDirectoryQuietly(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // Cleanup failures are non-fatal for module install flow.
        }
    }
}

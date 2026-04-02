using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using System.Xml.Linq;

namespace Kestrun.Tool;

internal static partial class Program
{
    /// <summary>
    /// Resolves the service runtime payload required for service install or run execution.
    /// </summary>
    /// <param name="runtimeSource">Optional runtime package source override.</param>
    /// <param name="runtimePackage">Optional explicit runtime package path.</param>
    /// <param name="runtimeVersion">Optional runtime package version override.</param>
    /// <param name="runtimePackageId">Optional runtime package id override.</param>
    /// <param name="runtimeCache">Optional runtime cache directory override.</param>
    /// <param name="requireModules">True when the resolved payload must also contain bundled modules.</param>
    /// <param name="allowToolDistributionFallback">True to allow falling back to the staged runtime bundled with Kestrun.Tool when no explicit package is available. Should be false for service install, where only a proper versioned runtime package is acceptable.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when a usable runtime payload is available.</returns>
    private static bool TryResolveServiceRuntimePackage(
        string? runtimeSource,
        string? runtimePackage,
        string? runtimeVersion,
        string? runtimePackageId,
        string? runtimeCache,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        bool requireModules,
        bool allowToolDistributionFallback,
        out ResolvedServiceRuntimePackage runtimePackageLayout,
        out string error)
    {
        runtimePackageLayout = default!;

        var hasExplicitRuntimeOverride = HasExplicitRuntimeOverride(
            runtimeSource,
            runtimePackage,
            runtimeVersion,
            runtimePackageId,
            runtimeCache);

        if (!TryGetServiceRuntimeRid(out var rid, out error))
        {
            return false;
        }

        var effectivePackageId = GetEffectiveRuntimePackageId(rid, runtimePackageId);
        var requestedVersion = NormalizeRuntimeVersion(runtimeVersion);

        if (!TryResolveRuntimeCacheRoot(runtimeCache, out var cacheRoot, out error))
        {
            return allowToolDistributionFallback && !hasExplicitRuntimeOverride && TryResolveServiceRuntimePackageFromToolDistribution(rid, requireModules, out runtimePackageLayout);
        }

        // When an explicit runtime package path is provided, attempt to resolve it directly without involving source or cache resolution,
        // since the intent is clear and this avoids unexpected results from misconfigured source/cache arguments.
        // If this fails, do not proceed with source/cache resolution to avoid further unexpected results and instead return the explicit resolution error to the user for corrective action.
        if (!string.IsNullOrWhiteSpace(runtimePackage))
        {
            return TryResolveServiceRuntimePackageFromExplicitPackage(
                rid,
                effectivePackageId,
                requestedVersion,
                runtimePackage,
                cacheRoot,
                requireModules,
                out runtimePackageLayout,
                out error);
        }

        // When a direct runtime source is provided, attempt to resolve it as a local package path or a direct URL before proceeding with normal cache/source resolution.
        // This allows users to specify direct overrides without needing to also configure the cache or sources, and avoids unexpected cache/source resolution when the intent is to use a specific provided package.
        if (TryResolveServiceRuntimePackageFromDirectSource(
                rid,
                effectivePackageId,
                requestedVersion,
                runtimeSource,
                cacheRoot,
                bearerToken,
                customHeaders,
                ignoreCertificate,
                requireModules,
                out runtimePackageLayout,
                out error,
                out var runtimeSourceWasDirect))
        {
            return true;
        }

        // If the runtime source was treated as a direct package source but failed to resolve, do not proceed with source/cache resolution to avoid unexpected results from misconfigured source arguments
        // (e.g. a URL missing the package file name or a directory missing the expected package file).
        if (runtimeSourceWasDirect)
        {
            return false;
        }

        // When no direct source overrides are provided or the direct source fails to resolve, proceed with normal resolution from cache and configured sources.
        return TryResolveServiceRuntimePackageFromCacheOrSources(
            rid,
            effectivePackageId,
            requestedVersion,
            runtimeSource,
            cacheRoot,
            bearerToken,
            customHeaders,
            ignoreCertificate,
            requireModules,
            allowToolDistributionFallback,
            hasExplicitRuntimeOverride,
            out runtimePackageLayout,
            out error);
    }

    /// <summary>
    /// Determines whether any runtime-resolution override was provided explicitly.
    /// </summary>
    /// <param name="runtimeSource">Optional runtime package source override.</param>
    /// <param name="runtimePackage">Optional explicit runtime package path.</param>
    /// <param name="runtimeVersion">Optional runtime package version override.</param>
    /// <param name="runtimePackageId">Optional runtime package id override.</param>
    /// <param name="runtimeCache">Optional runtime cache directory override.</param>
    /// <returns>True when at least one runtime override argument was supplied.</returns>
    private static bool HasExplicitRuntimeOverride(
        string? runtimeSource,
        string? runtimePackage,
        string? runtimeVersion,
        string? runtimePackageId,
        string? runtimeCache)
        => !string.IsNullOrWhiteSpace(runtimeSource)
            || !string.IsNullOrWhiteSpace(runtimePackage)
            || !string.IsNullOrWhiteSpace(runtimeVersion)
            || !string.IsNullOrWhiteSpace(runtimePackageId)
            || !string.IsNullOrWhiteSpace(runtimeCache);

    /// <summary>
    /// Resolves the effective runtime package id for the current RID.
    /// </summary>
    /// <param name="rid">Resolved runtime identifier.</param>
    /// <param name="runtimePackageId">Optional runtime package id override.</param>
    /// <returns>Effective package id.</returns>
    private static string GetEffectiveRuntimePackageId(string rid, string? runtimePackageId)
        => string.IsNullOrWhiteSpace(runtimePackageId)
            ? $"{RuntimePackageIdPrefix}.{rid}"
            : runtimePackageId.Trim();

    /// <summary>
    /// Normalizes an optional runtime package version override.
    /// </summary>
    /// <param name="runtimeVersion">Optional runtime package version override.</param>
    /// <returns>Trimmed runtime version, or null when unspecified.</returns>
    private static string? NormalizeRuntimeVersion(string? runtimeVersion)
        => string.IsNullOrWhiteSpace(runtimeVersion) ? null : runtimeVersion.Trim();

    /// <summary>
    /// Resolves a runtime payload using extracted cache entries, configured sources, and optional tool fallback.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="effectivePackageId">Effective package id.</param>
    /// <param name="requestedVersion">Optional requested package version override.</param>
    /// <param name="runtimeSource">Optional runtime source override.</param>
    /// <param name="cacheRoot">Resolved runtime cache root.</param>
    /// <param name="bearerToken">Optional bearer token for HTTP sources.</param>
    /// <param name="customHeaders">Optional custom headers for HTTP sources.</param>
    /// <param name="ignoreCertificate">True to allow insecure HTTPS for HTTP sources.</param>
    /// <param name="requireModules">True when bundled modules are required.</param>
    /// <param name="allowToolDistributionFallback">True to allow staged tool fallback when package acquisition fails.</param>
    /// <param name="hasExplicitRuntimeOverride">True when explicit runtime-resolution overrides were provided.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when a usable runtime payload is available.</returns>
    private static bool TryResolveServiceRuntimePackageFromCacheOrSources(
        string rid,
        string effectivePackageId,
        string? requestedVersion,
        string? runtimeSource,
        string cacheRoot,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        bool requireModules,
        bool allowToolDistributionFallback,
        bool hasExplicitRuntimeOverride,
        out ResolvedServiceRuntimePackage runtimePackageLayout,
        out string error)
    {
        runtimePackageLayout = default!;

        var effectiveVersion = string.IsNullOrWhiteSpace(requestedVersion)
            ? GetDefaultServiceRuntimePackageVersion()
            : requestedVersion;

        if (string.IsNullOrWhiteSpace(effectiveVersion))
        {
            error = "Unable to determine the default service runtime package version.";
            return false;
        }

        var sourceCandidates = GetServiceRuntimeSourceCandidates(runtimeSource);
        var errors = new List<string>();

        if (TryResolveServiceRuntimePackageFromExpandedCache(
                rid,
                effectivePackageId,
                effectiveVersion,
                cacheRoot,
                requireModules,
                out runtimePackageLayout,
                out error))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            errors.Add(error);
        }

        foreach (var sourceCandidate in sourceCandidates)
        {
            if (TryResolveServiceRuntimePackageFromSource(
                    rid,
                    effectivePackageId,
                    effectiveVersion,
                    sourceCandidate,
                    cacheRoot,
                    bearerToken,
                    customHeaders,
                    ignoreCertificate,
                    requireModules,
                    out runtimePackageLayout,
                    out error))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                errors.Add(error);
            }
        }

        if (allowToolDistributionFallback && !hasExplicitRuntimeOverride && TryResolveServiceRuntimePackageFromToolDistribution(rid, requireModules, out runtimePackageLayout))
        {
            if (errors.Count > 0)
            {
                Console.Error.WriteLine("Warning: unable to acquire the service runtime package from cache/NuGet; falling back to the staged Kestrun.Tool runtime payload.");
                Console.Error.WriteLine($"  {errors[0]}");
            }

            return true;
        }

        error = BuildRuntimePackageNotFoundError(effectivePackageId, effectiveVersion, rid, allowToolDistributionFallback, errors);
        return false;
    }

    /// <summary>
    /// Builds a user-facing error message when no usable runtime package could be located.
    /// </summary>
    /// <param name="packageId">Requested package id.</param>
    /// <param name="packageVersion">Requested package version.</param>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="allowToolDistributionFallback">Whether the tool-distribution fallback was attempted.</param>
    /// <param name="errors">Accumulated acquisition errors.</param>
    /// <returns>Formatted error message.</returns>
    private static string BuildRuntimePackageNotFoundError(
        string packageId,
        string packageVersion,
        string rid,
        bool allowToolDistributionFallback,
        IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            var message = $"Unable to locate service runtime package '{packageId}' version '{packageVersion}' for RID '{rid}'.";
            if (!allowToolDistributionFallback)
            {
                message += $" Use --runtime-package <path> to specify a local package file, or --runtime-version to target a published version.";
            }

            return message;
        }

        var baseError = string.Join(Environment.NewLine, errors.Distinct(StringComparer.Ordinal));
        if (!allowToolDistributionFallback)
        {
            baseError += $"{Environment.NewLine}Use --runtime-package <path> to specify a local '{packageId}' package file, or --runtime-version to target a different published version.";
        }

        return baseError;
    }

    /// <summary>
    /// Resolves the currently staged runtime payload from the tool distribution when present.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="requireModules">True when bundled modules are required.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <returns>True when the staged runtime payload is present.</returns>
    private static bool TryResolveServiceRuntimePackageFromToolDistribution(
        string rid,
        bool requireModules,
        out ResolvedServiceRuntimePackage runtimePackageLayout)
    {
        runtimePackageLayout = default!;
        if (!TryResolveDedicatedServiceHostExecutableFromToolDistribution(out var serviceHostExecutablePath))
        {
            return false;
        }

        var modulesPath = string.Empty;
        if (requireModules && !TryResolvePowerShellModulesPayloadFromToolDistribution(out modulesPath))
        {
            return false;
        }

        var extractionRoot = Path.GetDirectoryName(serviceHostExecutablePath) ?? string.Empty;
        runtimePackageLayout = new ResolvedServiceRuntimePackage(
            rid,
            $"{RuntimePackageIdPrefix}.{rid}",
            GetDefaultServiceRuntimePackageVersion(),
            string.Empty,
            extractionRoot,
            serviceHostExecutablePath,
            modulesPath);
        return true;
    }

    /// <summary>
    /// Resolves a runtime payload from an explicit local runtime package path.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="expectedPackageId">Expected package id.</param>
    /// <param name="expectedVersion">Expected package version.</param>
    /// <param name="runtimePackage">Explicit package path.</param>
    /// <param name="cacheRoot">Resolved cache root path.</param>
    /// <param name="requireModules">True when bundled modules are required.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the explicit package path resolves successfully.</returns>
    private static bool TryResolveServiceRuntimePackageFromExplicitPackage(
        string rid,
        string expectedPackageId,
        string? expectedVersion,
        string runtimePackage,
        string cacheRoot,
        bool requireModules,
        out ResolvedServiceRuntimePackage runtimePackageLayout,
        out string error)
    {
        runtimePackageLayout = default!;

        if (!TryLoadAndValidateExplicitRuntimePackage(
                rid,
                expectedPackageId,
                expectedVersion,
                runtimePackage,
                out var packagePath,
                out var packageBytes,
                out var packageId,
                out var packageVersion,
                out error))
        {
            return false;
        }

        var effectivePackagePath = TryPrepareExplicitRuntimePackageCacheEntry(
            packagePath,
            cacheRoot,
            packageId,
            packageVersion,
            out _);

        var packageHash = Convert.ToHexString(SHA256.HashData(packageBytes))[..12].ToLowerInvariant();
        var extractionRoot = Path.Combine(cacheRoot, "expanded", SanitizePathToken(packageId), $"{packageVersion}-{packageHash}");
        return TryPrepareResolvedServiceRuntimePackage(
            rid,
            packageId,
            packageVersion,
            effectivePackagePath,
            packageBytes,
            extractionRoot,
            requireModules,
            out runtimePackageLayout,
            out error);
    }

    /// <summary>
    /// Loads an explicit runtime package and validates its identity metadata against expected values.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="expectedPackageId">Expected package id.</param>
    /// <param name="expectedVersion">Optional expected package version.</param>
    /// <param name="runtimePackage">Explicit runtime package path.</param>
    /// <param name="packagePath">Resolved absolute runtime package path.</param>
    /// <param name="packageBytes">Runtime package bytes.</param>
    /// <param name="packageId">Resolved package id.</param>
    /// <param name="packageVersion">Resolved package version.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when the explicit runtime package path exists and matches expected identity metadata.</returns>
    private static bool TryLoadAndValidateExplicitRuntimePackage(
        string rid,
        string expectedPackageId,
        string? expectedVersion,
        string runtimePackage,
        out string packagePath,
        out byte[] packageBytes,
        out string packageId,
        out string packageVersion,
        out string error)
    {
        packageBytes = [];
        packageId = string.Empty;
        packageVersion = string.Empty;

        if (!TryResolveExplicitRuntimePackagePath(runtimePackage, expectedPackageId, expectedVersion, out packagePath, out error))
        {
            return false;
        }

        packageBytes = File.ReadAllBytes(packagePath);
        if (!TryReadPackageIdentity(packageBytes, out packageId, out packageVersion))
        {
            error = $"Runtime package '{packagePath}' does not contain a readable nuspec id/version.";
            return false;
        }

        if (!string.Equals(packageId, expectedPackageId, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Runtime package '{packagePath}' has package id '{packageId}', but '{expectedPackageId}' was expected for RID '{rid}'.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedVersion)
            && !string.Equals(packageVersion, expectedVersion, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Runtime package '{packagePath}' has version '{packageVersion}', but '{expectedVersion}' was expected.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves an explicit runtime package argument to a concrete package file path.
    /// </summary>
    /// <param name="runtimePackage">Explicit runtime package file or directory path.</param>
    /// <param name="expectedPackageId">Expected runtime package id.</param>
    /// <param name="expectedVersion">Optional expected runtime package version.</param>
    /// <param name="packagePath">Resolved runtime package file path.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the explicit runtime package argument resolves to a readable package file.</returns>
    private static bool TryResolveExplicitRuntimePackagePath(
        string runtimePackage,
        string expectedPackageId,
        string? expectedVersion,
        out string packagePath,
        out string error)
    {
        packagePath = Path.GetFullPath(runtimePackage);
        error = string.Empty;

        if (Directory.Exists(packagePath))
        {
            return TryResolveExplicitRuntimePackageFromDirectory(packagePath, expectedPackageId, expectedVersion, out packagePath, out error);
        }

        if (!File.Exists(packagePath))
        {
            error = runtimePackage.EndsWith(RuntimePackageExtension, StringComparison.OrdinalIgnoreCase)
                ? $"Runtime package file was not found: {packagePath}"
                : $"Runtime package path was not found: {packagePath}";
            return false;
        }

        if (!packagePath.EndsWith(RuntimePackageExtension, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Runtime package file '{packagePath}' must point to a '{RuntimePackageExtension}' package.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Resolves the expected runtime package file from a directory passed to <c>--runtime-package</c>.
    /// </summary>
    /// <param name="runtimePackageDirectory">Directory containing runtime packages.</param>
    /// <param name="expectedPackageId">Expected runtime package id.</param>
    /// <param name="expectedVersion">Optional expected runtime package version.</param>
    /// <param name="packagePath">Resolved runtime package file path.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the directory contains the expected runtime package file.</returns>
    private static bool TryResolveExplicitRuntimePackageFromDirectory(
        string runtimePackageDirectory,
        string expectedPackageId,
        string? expectedVersion,
        out string packagePath,
        out string error)
    {
        var effectiveVersion = string.IsNullOrWhiteSpace(expectedVersion)
            ? GetDefaultServiceRuntimePackageVersion()
            : expectedVersion;
        var expectedFileName = $"{expectedPackageId}.{effectiveVersion}{RuntimePackageExtension}";

        packagePath = Directory
            .EnumerateFiles(runtimePackageDirectory, $"*{RuntimePackageExtension}", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), expectedFileName, StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(packagePath))
        {
            error = string.Empty;
            return true;
        }

        error = $"Runtime package '{expectedFileName}' was not found in folder '{runtimePackageDirectory}'.";
        return false;
    }

    /// <summary>
    /// Creates or reuses the structured cache entry for an explicit runtime package.
    /// </summary>
    /// <param name="packagePath">Resolved source package path.</param>
    /// <param name="cacheRoot">Resolved runtime cache root.</param>
    /// <param name="packageId">Runtime package id.</param>
    /// <param name="packageVersion">Runtime package version.</param>
    /// <param name="cachedPackagePath">Structured cache package path.</param>
    /// <returns>Path to use for subsequent package resolution (cached path when present; otherwise original package path).</returns>
    private static string TryPrepareExplicitRuntimePackageCacheEntry(
        string packagePath,
        string cacheRoot,
        string packageId,
        string packageVersion,
        out string cachedPackagePath)
    {
        var packageFileName = $"{packageId}.{packageVersion}{RuntimePackageExtension}";
        cachedPackagePath = Path.Combine(cacheRoot, "packages", SanitizePathToken(packageId), packageVersion, packageFileName);
        var cachedPackageDirectory = Path.GetDirectoryName(cachedPackagePath);
        if (!string.IsNullOrWhiteSpace(cachedPackageDirectory))
        {
            _ = Directory.CreateDirectory(cachedPackageDirectory);
        }

        if (!File.Exists(cachedPackagePath))
        {
            try
            {
                File.Copy(packagePath, cachedPackagePath, overwrite: false);
            }
            catch (IOException)
            {
                // Another process may have created the cache entry concurrently.
            }
        }

        return File.Exists(cachedPackagePath) ? cachedPackagePath : packagePath;
    }

    /// <summary>
    /// Resolves a runtime payload from a source candidate and cache root.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="packageVersion">Package version.</param>
    /// <param name="sourceCandidate">Source candidate (directory or URL).</param>
    /// <param name="cacheRoot">Cache root path.</param>
    /// <param name="requireModules">True when bundled modules are required.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the source yields a usable runtime package.</returns>
    private static bool TryResolveServiceRuntimePackageFromSource(
        string rid,
        string packageId,
        string packageVersion,
        string sourceCandidate,
        string cacheRoot,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        bool requireModules,
        out ResolvedServiceRuntimePackage runtimePackageLayout,
        out string error)
    {
        runtimePackageLayout = default!;

        if (!TryEnsureCachedRuntimePackageForSource(
                sourceCandidate,
                packageId,
                packageVersion,
                cacheRoot,
                bearerToken,
                customHeaders,
                ignoreCertificate,
                out var cachedPackagePath,
                out error))
        {
            return false;
        }

        if (!TryLoadAndValidateCachedRuntimePackage(
                cachedPackagePath,
                packageId,
                packageVersion,
                out var packageBytes,
                out error))
        {
            return false;
        }

        var extractionRoot = Path.Combine(cacheRoot, "expanded", SanitizePathToken(packageId), packageVersion);
        return TryPrepareResolvedServiceRuntimePackage(
            rid,
            packageId,
            packageVersion,
            cachedPackagePath,
            packageBytes,
            extractionRoot,
            requireModules,
            out runtimePackageLayout,
            out error);
    }

    /// <summary>
    /// Ensures a source-provided runtime package exists in the structured cache location.
    /// </summary>
    /// <param name="sourceCandidate">Source candidate (directory or URL).</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="packageVersion">Package version.</param>
    /// <param name="cacheRoot">Cache root path.</param>
    /// <param name="bearerToken">Optional bearer token for HTTP sources.</param>
    /// <param name="customHeaders">Optional custom headers for HTTP sources.</param>
    /// <param name="ignoreCertificate">True to allow insecure HTTPS for HTTP sources.</param>
    /// <param name="cachedPackagePath">Resolved structured cache package path.</param>
    /// <param name="error">Acquisition error details.</param>
    /// <returns>True when the requested package is present in the structured cache path.</returns>
    private static bool TryEnsureCachedRuntimePackageForSource(
        string sourceCandidate,
        string packageId,
        string packageVersion,
        string cacheRoot,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        out string cachedPackagePath,
        out string error)
    {
        error = string.Empty;
        var packageFileName = $"{packageId}.{packageVersion}{RuntimePackageExtension}";
        cachedPackagePath = Path.Combine(cacheRoot, "packages", SanitizePathToken(packageId), packageVersion, packageFileName);
        if (File.Exists(cachedPackagePath))
        {
            return true;
        }

        var packageDirectory = Path.GetDirectoryName(cachedPackagePath);
        if (!string.IsNullOrWhiteSpace(packageDirectory))
        {
            _ = Directory.CreateDirectory(packageDirectory);
        }

        // Before downloading, check whether the package was placed flat in the cache root
        // (e.g. copied there manually or left by an earlier tool version). If so, migrate it
        // to the structured location so subsequent lookups use the normal path.
        var flatPackagePath = Path.Combine(cacheRoot, packageFileName);
        if (File.Exists(flatPackagePath))
        {
            try
            {
                File.Copy(flatPackagePath, cachedPackagePath, overwrite: false);
            }
            catch (IOException)
            {
                // Another process may have created it concurrently; continue with whatever is there.
            }
        }

        if (File.Exists(cachedPackagePath))
        {
            return true;
        }

        if (TryAcquireRuntimePackageFromSource(sourceCandidate, packageId, packageVersion, cachedPackagePath, bearerToken, customHeaders, ignoreCertificate, out error))
        {
            return true;
        }

        TryCleanupEmptyRuntimePackageDirectory(packageDirectory, cacheRoot);
        return false;
    }

    /// <summary>
    /// Loads and validates identity metadata for a cached runtime package.
    /// </summary>
    /// <param name="cachedPackagePath">Structured cache package path.</param>
    /// <param name="packageId">Expected package id.</param>
    /// <param name="packageVersion">Expected package version.</param>
    /// <param name="packageBytes">Cached package bytes.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when the cached package exists and matches the requested id/version.</returns>
    private static bool TryLoadAndValidateCachedRuntimePackage(
        string cachedPackagePath,
        string packageId,
        string packageVersion,
        out byte[] packageBytes,
        out string error)
    {
        packageBytes = File.ReadAllBytes(cachedPackagePath);
        if (!TryReadPackageIdentity(packageBytes, out var resolvedPackageId, out var resolvedPackageVersion))
        {
            error = $"Runtime package '{cachedPackagePath}' does not contain a readable nuspec id/version.";
            return false;
        }

        if (!string.Equals(resolvedPackageId, packageId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(resolvedPackageVersion, packageVersion, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Runtime package '{cachedPackagePath}' resolved to '{resolvedPackageId}' version '{resolvedPackageVersion}', but '{packageId}' version '{packageVersion}' was requested.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Attempts to resolve a runtime payload from a previously extracted cache entry.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="packageVersion">Package version.</param>
    /// <param name="cacheRoot">Cache root path.</param>
    /// <param name="requireModules">True when bundled modules are required.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when a compatible extracted runtime payload is found.</returns>
    private static bool TryResolveServiceRuntimePackageFromExpandedCache(
        string rid,
        string packageId,
        string packageVersion,
        string cacheRoot,
        bool requireModules,
        out ResolvedServiceRuntimePackage runtimePackageLayout,
        out string error)
    {
        runtimePackageLayout = default!;
        error = string.Empty;

        var expandedPackageRoot = Path.Combine(cacheRoot, "expanded", SanitizePathToken(packageId));
        if (!Directory.Exists(expandedPackageRoot))
        {
            return false;
        }

        var versionCandidates = new List<string>();
        var exactVersionRoot = Path.Combine(expandedPackageRoot, packageVersion);
        if (Directory.Exists(exactVersionRoot))
        {
            versionCandidates.Add(exactVersionRoot);
        }

        versionCandidates.AddRange(
            Directory.EnumerateDirectories(expandedPackageRoot, $"{packageVersion}-*", SearchOption.TopDirectoryOnly)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase));

        foreach (var extractionRoot in versionCandidates)
        {
            if (TryResolveExtractedServiceRuntimePackageLayout(
                    rid,
                    packageId,
                    packageVersion,
                    packagePath: string.Empty,
                    extractionRoot,
                    requireModules,
                    out runtimePackageLayout,
                    out error))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Ensures an extracted runtime package is present and resolves its host/modules layout.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="packageVersion">Package version.</param>
    /// <param name="packagePath">Package file path.</param>
    /// <param name="packageBytes">Package bytes.</param>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="requireModules">True when bundled modules are required.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when extraction and layout resolution succeed.</returns>
    private static bool TryPrepareResolvedServiceRuntimePackage(
        string rid,
        string packageId,
        string packageVersion,
        string packagePath,
        byte[] packageBytes,
        string extractionRoot,
        bool requireModules,
        out ResolvedServiceRuntimePackage runtimePackageLayout,
        out string error)
    {
        runtimePackageLayout = default!;
        if (!TryEnsureExtractedServiceRuntimePackage(packageBytes, extractionRoot, out error))
        {
            return false;
        }
        // The extracted layout is expected to contain the service host executable at the root of the package, with bundled modules in a 'modules' subdirectory when present. This is validated and enforced by the runtime package creation process, but is not guaranteed for arbitrary packages, so the presence of these components is verified before returning a resolved layout.
        return TryResolveExtractedServiceRuntimePackageLayout(
                rid,
                packageId,
                packageVersion,
                packagePath,
                extractionRoot,
                requireModules,
                out runtimePackageLayout,
                out error);
    }

    /// <summary>
    /// Resolves the runtime package source candidates in priority order.
    /// </summary>
    /// <param name="runtimeSource">Optional explicit source override.</param>
    /// <returns>Ordered source candidates.</returns>
    private static IReadOnlyList<string> GetServiceRuntimeSourceCandidates(string? runtimeSource) =>
    !string.IsNullOrWhiteSpace(runtimeSource) ? [runtimeSource.Trim()] : [DefaultNuGetServiceIndexUrl];

    /// <summary>
    /// Resolves the effective cache root used for runtime packages.
    /// </summary>
    /// <param name="runtimeCache">Optional cache root override.</param>
    /// <param name="cacheRoot">Resolved cache root.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the cache root is usable.</returns>
    private static bool TryResolveRuntimeCacheRoot(string? runtimeCache, out string cacheRoot, out string error)
    {
        cacheRoot = string.Empty;
        error = string.Empty;

        var candidateDisplay = string.IsNullOrWhiteSpace(runtimeCache)
            ? GetDefaultRuntimeCacheRoot()
            : runtimeCache;

        try
        {
            var candidate = string.IsNullOrWhiteSpace(runtimeCache)
                ? candidateDisplay
                : Path.GetFullPath(runtimeCache);

            _ = Directory.CreateDirectory(candidate);
            cacheRoot = candidate;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Unable to use runtime cache directory '{candidateDisplay}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Returns the default runtime cache root for the current platform.
    /// </summary>
    /// <returns>Default cache root path.</returns>
    private static string GetDefaultRuntimeCacheRoot()
    {
        // On Windows, use the machine-wide common application data directory so that the cache is shared
        // across all users without requiring per-user downloads.
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Kestrun", "RuntimePackages");
        }

        // On Unix-based platforms, system-wide cache directories (/Library/Caches, /var/cache) are typically
        // not writable by non-root users, so fall back to the per-user cache directory — consistent with the
        // approach used for service deployment roots on the same platforms.
        if (OperatingSystem.IsMacOS())
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                return Path.Combine(userProfile, "Library", "Caches", "Kestrun", "RuntimePackages");
            }
        }

        // On Linux, honour XDG_CACHE_HOME when set; otherwise fall back to ~/.cache, which is the XDG default.
        var xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdgCacheHome))
        {
            return Path.Combine(xdgCacheHome, "kestrun", "runtime-packages");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(home, ".cache", "kestrun", "runtime-packages");
        }

        // Ultimate fallback: /tmp is always writable and avoids a hard failure on unusual setups.
        return Path.Combine(Path.GetTempPath(), "kestrun", "runtime-packages");
    }

    /// <summary>
    /// Resolves a runtime payload from a direct source package path or URL.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="expectedPackageId">Expected package id.</param>
    /// <param name="expectedVersion">Optional expected package version.</param>
    /// <param name="runtimeSource">Explicit runtime source candidate.</param>
    /// <param name="cacheRoot">Resolved cache root path.</param>
    /// <param name="bearerToken">Optional bearer token for HTTP sources.</param>
    /// <param name="customHeaders">Optional custom headers for HTTP sources.</param>
    /// <param name="ignoreCertificate">True to allow insecure HTTPS for HTTP sources.</param>
    /// <param name="requireModules">True when bundled modules are required.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <param name="error">Resolution error details.</param>
    /// <param name="runtimeSourceWasDirect">True when the runtime source was treated as a direct package source.</param>
    /// <returns>True when a direct runtime source resolves successfully.</returns>
    private static bool TryResolveServiceRuntimePackageFromDirectSource(
        string rid,
        string expectedPackageId,
        string? expectedVersion,
        string? runtimeSource,
        string cacheRoot,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        bool requireModules,
        out ResolvedServiceRuntimePackage runtimePackageLayout,
        out string error,
        out bool runtimeSourceWasDirect)
    {
        runtimePackageLayout = default!;
        error = string.Empty;
        runtimeSourceWasDirect = false;

        if (string.IsNullOrWhiteSpace(runtimeSource))
        {
            return false;
        }

        var trimmedSource = runtimeSource.Trim();
        if (TryResolveDirectRuntimeSourceLocalPackagePath(trimmedSource, out var localPackagePath, out runtimeSourceWasDirect, out error))
        {
            return TryResolveServiceRuntimePackageFromExplicitPackage(
                rid,
                expectedPackageId,
                expectedVersion,
                localPackagePath,
                cacheRoot,
                requireModules,
                out runtimePackageLayout,
                out error);
        }

        if (runtimeSourceWasDirect)
        {
            return false;
        }

        if (TryParseServiceContentRootHttpUri(trimmedSource, out var packageUri) && IsDirectRuntimePackageUri(packageUri))
        {
            runtimeSourceWasDirect = true;
            var downloadPath = GetDirectRuntimePackageDownloadPath(cacheRoot, expectedPackageId, expectedVersion, packageUri);
            var downloadDirectory = Path.GetDirectoryName(downloadPath);
            if (!string.IsNullOrWhiteSpace(downloadDirectory))
            {
                _ = Directory.CreateDirectory(downloadDirectory);
            }

            return (File.Exists(downloadPath)
                || TryDownloadRuntimePackageFile(packageUri, downloadPath, bearerToken, customHeaders, ignoreCertificate, out error)) && TryResolveServiceRuntimePackageFromExplicitPackage(
                rid,
                expectedPackageId,
                expectedVersion,
                downloadPath,
                cacheRoot,
                requireModules,
                out runtimePackageLayout,
                out error);
        }

        return false;
    }

    /// <summary>
    /// Returns the default runtime package version based on the running tool version.
    /// </summary>
    /// <returns>Normalized package version string.</returns>
    private static string GetDefaultServiceRuntimePackageVersion()
    {
        var version = GetProductVersion();
        var plusIndex = version.IndexOf('+');
        return plusIndex >= 0 ? version[..plusIndex] : version;
    }

    /// <summary>
    /// Downloads or copies a runtime package from the provided source into the cache path.
    /// </summary>
    /// <param name="sourceCandidate">Source candidate.</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="packageVersion">Package version.</param>
    /// <param name="destinationPath">Cached destination path.</param>
    /// <param name="error">Acquisition error details.</param>
    /// <returns>True when the package is acquired successfully.</returns>
    private static bool TryAcquireRuntimePackageFromSource(
        string sourceCandidate,
        string packageId,
        string packageVersion,
        string destinationPath,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        out string error)
    {
        if (TryResolveDirectRuntimeSourceLocalPackagePath(sourceCandidate, out var localPackagePath, out var runtimeSourceWasDirect, out error))
        {
            File.Copy(localPackagePath, destinationPath, overwrite: true);
            return true;
        }

        if (runtimeSourceWasDirect)
        {
            return false;
        }

        if (Directory.Exists(sourceCandidate))
        {
            return TryCopyRuntimePackageFromLocalSource(sourceCandidate, packageId, packageVersion, destinationPath, out error);
        }

        if (Uri.TryCreate(sourceCandidate, UriKind.Absolute, out var sourceUri)
            && (sourceUri.Scheme == Uri.UriSchemeHttp || sourceUri.Scheme == Uri.UriSchemeHttps))
        {
            return IsDirectRuntimePackageUri(sourceUri)
                ? TryDownloadRuntimePackageFile(sourceUri, destinationPath, bearerToken, customHeaders, ignoreCertificate, out error)
                : TryDownloadRuntimePackageFromSource(sourceUri, packageId, packageVersion, destinationPath, bearerToken, customHeaders, ignoreCertificate, out error);
        }

        error = $"Runtime source '{sourceCandidate}' is neither an existing directory, a readable '{RuntimePackageExtension}' file, nor an HTTP(S) source URL.";
        return false;
    }

    /// <summary>
    /// Resolves a direct local runtime package source from a file path or file URI.
    /// </summary>
    /// <param name="sourceCandidate">Source candidate to inspect.</param>
    /// <param name="packagePath">Resolved package path when the source is a direct local package.</param>
    /// <param name="runtimeSourceWasDirect">True when the source was interpreted as a direct package reference.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when a direct package path was resolved successfully.</returns>
    private static bool TryResolveDirectRuntimeSourceLocalPackagePath(
        string sourceCandidate,
        out string packagePath,
        out bool runtimeSourceWasDirect,
        out string error)
    {
        packagePath = string.Empty;
        error = string.Empty;
        runtimeSourceWasDirect = false;

        if (string.IsNullOrWhiteSpace(sourceCandidate))
        {
            return false;
        }

        if (Directory.Exists(sourceCandidate))
        {
            return false;
        }

        if (TryResolveDirectRuntimeSourceExistingFilePath(sourceCandidate, out packagePath, out runtimeSourceWasDirect, out error))
        {
            return true;
        }

        if (runtimeSourceWasDirect)
        {
            return false;
        }

        if (TryResolveDirectRuntimeSourceFileUri(sourceCandidate, out packagePath, out runtimeSourceWasDirect, out error))
        {
            return true;
        }

        if (runtimeSourceWasDirect)
        {
            return false;
        }

        if (sourceCandidate.EndsWith(RuntimePackageExtension, StringComparison.OrdinalIgnoreCase)
            && !Directory.Exists(sourceCandidate)
            && !TryParseServiceContentRootHttpUri(sourceCandidate, out _))
        {
            runtimeSourceWasDirect = true;
            error = $"Runtime package file was not found: {Path.GetFullPath(sourceCandidate)}";
            return false;
        }

        return false;
    }

    /// <summary>
    /// Resolves an existing local file path as a direct runtime package source.
    /// </summary>
    /// <param name="sourceCandidate">Source candidate to inspect.</param>
    /// <param name="packagePath">Resolved package path when successful.</param>
    /// <param name="runtimeSourceWasDirect">True when the source is a direct file reference.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the candidate resolves to an existing local package file.</returns>
    private static bool TryResolveDirectRuntimeSourceExistingFilePath(
        string sourceCandidate,
        out string packagePath,
        out bool runtimeSourceWasDirect,
        out string error)
    {
        packagePath = string.Empty;
        error = string.Empty;
        runtimeSourceWasDirect = false;

        if (!File.Exists(sourceCandidate))
        {
            return false;
        }

        runtimeSourceWasDirect = true;
        if (!sourceCandidate.EndsWith(RuntimePackageExtension, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Runtime source file '{sourceCandidate}' must point to a '{RuntimePackageExtension}' package.";
            return false;
        }

        packagePath = Path.GetFullPath(sourceCandidate);
        return true;
    }

    /// <summary>
    /// Resolves a file URI as a direct runtime package source.
    /// </summary>
    /// <param name="sourceCandidate">Source candidate to inspect.</param>
    /// <param name="packagePath">Resolved package path when successful.</param>
    /// <param name="runtimeSourceWasDirect">True when the source was interpreted as a direct file URI reference.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the source resolves to a valid local package file URI.</returns>
    private static bool TryResolveDirectRuntimeSourceFileUri(
        string sourceCandidate,
        out string packagePath,
        out bool runtimeSourceWasDirect,
        out string error)
    {
        packagePath = string.Empty;
        error = string.Empty;
        runtimeSourceWasDirect = false;

        if (!Uri.TryCreate(sourceCandidate, UriKind.Absolute, out var sourceUri) || !sourceUri.IsFile)
        {
            return false;
        }

        runtimeSourceWasDirect = true;
        var localPath = sourceUri.LocalPath;
        if (Directory.Exists(localPath))
        {
            runtimeSourceWasDirect = false;
            return false;
        }

        if (!localPath.EndsWith(RuntimePackageExtension, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Runtime source file '{sourceCandidate}' must point to a '{RuntimePackageExtension}' package.";
            return false;
        }

        if (!File.Exists(localPath))
        {
            error = $"Runtime package file was not found: {localPath}";
            return false;
        }

        packagePath = Path.GetFullPath(localPath);
        return true;
    }

    /// <summary>
    /// Determines whether a runtime source URI points directly at a package file.
    /// </summary>
    /// <param name="sourceUri">Source URI.</param>
    /// <returns>True when the URI points to a .nupkg payload.</returns>
    private static bool IsDirectRuntimePackageUri(Uri sourceUri)
        => sourceUri.AbsolutePath.EndsWith(RuntimePackageExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the cache path used for direct runtime package downloads.
    /// </summary>
    /// <param name="cacheRoot">Resolved cache root path.</param>
    /// <param name="packageId">Expected package id.</param>
    /// <param name="packageVersion">Optional expected package version.</param>
    /// <param name="sourceUri">Source package URI.</param>
    /// <returns>Cached download path.</returns>
    private static string GetDirectRuntimePackageDownloadPath(string cacheRoot, string packageId, string? packageVersion, Uri sourceUri)
    {
        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceUri.AbsoluteUri)))[..12].ToLowerInvariant();
        var sourceFileName = Path.GetFileName(Uri.UnescapeDataString(sourceUri.AbsolutePath));
        var safeFileStem = SanitizePathToken(string.IsNullOrWhiteSpace(sourceFileName) ? packageId : Path.GetFileNameWithoutExtension(sourceFileName));
        var fileName = string.IsNullOrWhiteSpace(safeFileStem)
            ? $"{SanitizePathToken(packageId)}{RuntimePackageExtension}"
            : $"{safeFileStem}{RuntimePackageExtension}";
        var versionFolder = string.IsNullOrWhiteSpace(packageVersion) ? "floating" : SanitizePathToken(packageVersion);
        return Path.Combine(cacheRoot, "downloads", SanitizePathToken(packageId), versionFolder, sourceHash, fileName);
    }

    /// <summary>
    /// Copies a runtime package from a local source directory.
    /// </summary>
    /// <param name="sourceDirectory">Source directory path.</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="packageVersion">Package version.</param>
    /// <param name="destinationPath">Cached destination path.</param>
    /// <param name="error">Copy error details.</param>
    /// <returns>True when the package is copied successfully.</returns>
    private static bool TryCopyRuntimePackageFromLocalSource(
        string sourceDirectory,
        string packageId,
        string packageVersion,
        string destinationPath,
        out string error)
    {
        error = string.Empty;
        var expectedFileName = $"{packageId}.{packageVersion}{RuntimePackageExtension}";
        var sourcePath = Directory
            .EnumerateFiles(sourceDirectory, $"*{RuntimePackageExtension}", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), expectedFileName, StringComparison.OrdinalIgnoreCase));

        if (sourcePath is null)
        {
            error = $"Runtime package '{expectedFileName}' was not found in local source '{sourceDirectory}'.";
            return false;
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
        return true;
    }

    /// <summary>
    /// Removes empty runtime package cache directories left behind by a failed acquisition attempt.
    /// </summary>
    /// <param name="packageDirectory">Package-version directory created for the attempted acquisition.</param>
    /// <param name="cacheRoot">Resolved runtime cache root.</param>
    private static void TryCleanupEmptyRuntimePackageDirectory(string? packageDirectory, string cacheRoot)
    {
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return;
        }

        try
        {
            var packagesRoot = Path.Combine(cacheRoot, "packages");
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var currentDirectory = Path.GetFullPath(packageDirectory);
            var normalizedPackagesRoot = Path.GetFullPath(packagesRoot);

            while (currentDirectory.StartsWith(normalizedPackagesRoot, comparison) && Directory.Exists(currentDirectory))
            {
                if (Directory.EnumerateFileSystemEntries(currentDirectory).Any())
                {
                    break;
                }

                Directory.Delete(currentDirectory, recursive: false);
                if (string.Equals(currentDirectory, normalizedPackagesRoot, comparison))
                {
                    break;
                }

                currentDirectory = Path.GetDirectoryName(currentDirectory) ?? string.Empty;
            }
        }
        catch
        {
            // Best-effort cleanup only. Runtime package resolution should continue even when cache cleanup fails.
        }
    }

    /// <summary>
    /// Downloads a runtime package from a NuGet v3 source or flat-container base URL.
    /// </summary>
    /// <param name="sourceUri">Source URI.</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="packageVersion">Package version.</param>
    /// <param name="destinationPath">Cached destination path.</param>
    /// <param name="error">Download error details.</param>
    /// <returns>True when the package is downloaded successfully.</returns>
    private static bool TryDownloadRuntimePackageFromSource(
        Uri sourceUri,
        string packageId,
        string packageVersion,
        string destinationPath,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        out string error)
    {
        if (!TryResolvePackageBaseAddress(sourceUri, bearerToken, customHeaders, ignoreCertificate, out var packageBaseAddress, out error))
        {
            return false;
        }

        var lowerPackageId = packageId.ToLowerInvariant();
        var lowerPackageVersion = packageVersion.ToLowerInvariant();
        var downloadUri = new Uri(packageBaseAddress, $"{lowerPackageId}/{lowerPackageVersion}/{lowerPackageId}.{lowerPackageVersion}{RuntimePackageExtension}");

        return TryDownloadRuntimePackageFile(downloadUri, destinationPath, bearerToken, customHeaders, ignoreCertificate, out error);
    }

    /// <summary>
    /// Downloads a runtime package file to a local destination path.
    /// </summary>
    /// <param name="packageUri">Package URI.</param>
    /// <param name="destinationPath">Destination file path.</param>
    /// <param name="bearerToken">Optional bearer token for HTTP requests.</param>
    /// <param name="customHeaders">Optional custom headers for HTTP requests.</param>
    /// <param name="ignoreCertificate">True to allow insecure HTTPS downloads.</param>
    /// <param name="error">Download error details.</param>
    /// <returns>True when the file is downloaded successfully.</returns>
    private static bool TryDownloadRuntimePackageFile(
        Uri packageUri,
        string destinationPath,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        out string error)
    {
        error = string.Empty;
        if (ignoreCertificate && !packageUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "--content-root-ignore-certificate is only valid for HTTPS URLs.";
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, packageUri);
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            if (!TryApplyServiceContentRootCustomHeaders(request, customHeaders, out error))
            {
                return false;
            }

            if (!ignoreCertificate)
            {
                using var response = ServiceContentRootHttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    error = $"Failed to download runtime package from '{packageUri}'. HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
                    return false;
                }

                using var sourceStream = response.Content.ReadAsStream();
                using var destinationStream = File.Create(destinationPath);
                sourceStream.CopyTo(destinationStream);
                return true;
            }

            using var insecureHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
            using var insecureClient = new HttpClient(insecureHandler)
            {
                Timeout = TimeSpan.FromMinutes(5),
            };
            insecureClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ProductName, "1.0"));
            using var insecureResponse = insecureClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
            if (!insecureResponse.IsSuccessStatusCode)
            {
                error = $"Failed to download runtime package from '{packageUri}'. HTTP {(int)insecureResponse.StatusCode} {insecureResponse.ReasonPhrase}.";
                return false;
            }

            using var insecureSourceStream = insecureResponse.Content.ReadAsStream();
            using var insecureDestinationStream = File.Create(destinationPath);
            insecureSourceStream.CopyTo(insecureDestinationStream);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to download runtime package from '{packageUri}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Resolves a package base address from a NuGet service index or direct flat-container base URI.
    /// </summary>
    /// <param name="sourceUri">Source URI.</param>
    /// <param name="packageBaseAddress">Resolved package base address.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the package base address is resolved.</returns>
    private static bool TryResolvePackageBaseAddress(
        Uri sourceUri,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        out Uri packageBaseAddress,
        out string error)
    {
        if (!TryResolveFlatContainerBaseAddress(sourceUri, out packageBaseAddress))
        {
            error = string.Empty;
            return true;
        }

        try
        {
            if (!TryGetRuntimeSourceJsonDocument(sourceUri, bearerToken, customHeaders, ignoreCertificate, out var document, out error))
            {
                packageBaseAddress = null!;
                return false;
            }

            using (document)
            {
                return TryResolvePackageBaseAddressFromServiceIndexDocument(sourceUri, document, out packageBaseAddress, out error);
            }
        }
        catch (Exception ex)
        {
            packageBaseAddress = null!;
            error = $"Failed to read NuGet service index '{sourceUri}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Determines whether a source URI can be treated as a direct flat-container package base address.
    /// </summary>
    /// <param name="sourceUri">Source URI.</param>
    /// <param name="packageBaseAddress">Resolved package base address when the source URI is a flat-container base address.</param>
    /// <returns>True when the source URI is a flat-container base address; otherwise, false to indicate the source should be treated as a service index.</returns>
    private static bool TryResolveFlatContainerBaseAddress(Uri sourceUri, out Uri packageBaseAddress)
    {
        packageBaseAddress = null!;
        if (sourceUri.AbsolutePath.EndsWith("index.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var absoluteUri = sourceUri.AbsoluteUri.EndsWith('/') ? sourceUri.AbsoluteUri : $"{sourceUri.AbsoluteUri}/";
        packageBaseAddress = new Uri(absoluteUri, UriKind.Absolute);
        return false;
    }

    /// <summary>
    /// Resolves the package base address from a NuGet service index document.
    /// </summary>
    /// <param name="sourceUri">Source URI for error context.</param>
    /// <param name="document">Parsed service index document.</param>
    /// <param name="packageBaseAddress">Resolved package base address.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when a valid PackageBaseAddress resource is found.</returns>
    private static bool TryResolvePackageBaseAddressFromServiceIndexDocument(
        Uri sourceUri,
        JsonDocument document,
        out Uri packageBaseAddress,
        out string error)
    {
        packageBaseAddress = null!;

        if (!TryGetRuntimeSourceResourcesArray(sourceUri, document, out var resources, out error))
        {
            return false;
        }

        if (TryResolvePackageBaseAddressFromResources(resources, out packageBaseAddress))
        {
            error = string.Empty;
            return true;
        }

        error = $"NuGet service index '{sourceUri}' does not advertise a PackageBaseAddress resource.";
        return false;
    }

    /// <summary>
    /// Reads and validates the resources array from a NuGet service index document.
    /// </summary>
    /// <param name="sourceUri">Source URI for error context.</param>
    /// <param name="document">Parsed service index document.</param>
    /// <param name="resources">Resolved resources array element.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when a resources array is present.</returns>
    private static bool TryGetRuntimeSourceResourcesArray(
        Uri sourceUri,
        JsonDocument document,
        out JsonElement resources,
        out string error)
    {
        error = string.Empty;
        if (document.RootElement.TryGetProperty("resources", out resources) && resources.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        error = $"NuGet service index '{sourceUri}' does not contain a resources array.";
        return false;
    }

    /// <summary>
    /// Resolves the first PackageBaseAddress resource URI from a service index resources array.
    /// </summary>
    /// <param name="resources">Service index resources array.</param>
    /// <param name="packageBaseAddress">Resolved package base address.</param>
    /// <returns>True when a valid PackageBaseAddress URI is found.</returns>
    private static bool TryResolvePackageBaseAddressFromResources(JsonElement resources, out Uri packageBaseAddress)
    {
        packageBaseAddress = null!;
        foreach (var resource in resources.EnumerateArray())
        {
            if (!TryResolvePackageBaseAddressFromResource(resource, out packageBaseAddress))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves a PackageBaseAddress URI from a single service index resource entry.
    /// </summary>
    /// <param name="resource">Service index resource entry.</param>
    /// <param name="packageBaseAddress">Resolved package base address.</param>
    /// <returns>True when the resource represents PackageBaseAddress and contains a valid URI.</returns>
    private static bool TryResolvePackageBaseAddressFromResource(JsonElement resource, out Uri packageBaseAddress)
    {
        packageBaseAddress = null!;
        if (!resource.TryGetProperty("@id", out var idProperty) || !resource.TryGetProperty("@type", out var typeProperty))
        {
            return false;
        }

        if (!IsPackageBaseAddressResourceType(typeProperty))
        {
            return false;
        }

        var packageBase = idProperty.GetString();
        if (string.IsNullOrWhiteSpace(packageBase))
        {
            return false;
        }

        packageBaseAddress = new Uri(packageBase.EndsWith('/') ? packageBase : $"{packageBase}/", UriKind.Absolute);
        return true;
    }

    /// <summary>
    /// Determines whether a service index resource type entry represents PackageBaseAddress.
    /// </summary>
    /// <param name="typeProperty">Resource type property value.</param>
    /// <returns>True when the resource type indicates PackageBaseAddress.</returns>
    private static bool IsPackageBaseAddressResourceType(JsonElement typeProperty)
        => typeProperty.ValueKind switch
        {
            JsonValueKind.String => typeProperty.GetString()?.Contains("PackageBaseAddress", StringComparison.OrdinalIgnoreCase) == true,
            JsonValueKind.Array => typeProperty.EnumerateArray().Any(static entry =>
                entry.ValueKind == JsonValueKind.String
                && entry.GetString()!.Contains("PackageBaseAddress", StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };

    /// <summary>
    /// Downloads and parses a JSON document from a runtime source URL.
    /// </summary>
    /// <param name="sourceUri">Source URI.</param>
    /// <param name="bearerToken">Optional bearer token.</param>
    /// <param name="customHeaders">Optional custom headers.</param>
    /// <param name="ignoreCertificate">True to allow insecure HTTPS.</param>
    /// <param name="document">Parsed JSON document.</param>
    /// <param name="error">Download or parse error details.</param>
    /// <returns>True when the JSON document is available.</returns>
    private static bool TryGetRuntimeSourceJsonDocument(
        Uri sourceUri,
        string? bearerToken,
        string[] customHeaders,
        bool ignoreCertificate,
        out JsonDocument document,
        out string error)
    {
        document = null!;
        error = string.Empty;

        if (ignoreCertificate && !sourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "--content-root-ignore-certificate is only valid for HTTPS URLs.";
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            if (!TryApplyServiceContentRootCustomHeaders(request, customHeaders, out error))
            {
                return false;
            }

            if (!ignoreCertificate)
            {
                using var response = ServiceContentRootHttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    error = $"Failed to query NuGet service index '{sourceUri}'. HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";
                    return false;
                }

                using var responseStream = response.Content.ReadAsStream();
                document = JsonDocument.Parse(responseStream);
                return true;
            }

            using var insecureHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
            using var insecureClient = new HttpClient(insecureHandler)
            {
                Timeout = TimeSpan.FromMinutes(5),
            };
            insecureClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(ProductName, "1.0"));
            using var insecureResponse = insecureClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
            if (!insecureResponse.IsSuccessStatusCode)
            {
                error = $"Failed to query NuGet service index '{sourceUri}'. HTTP {(int)insecureResponse.StatusCode} {insecureResponse.ReasonPhrase}.";
                return false;
            }

            using var insecureResponseStream = insecureResponse.Content.ReadAsStream();
            document = JsonDocument.Parse(insecureResponseStream);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to read NuGet service index '{sourceUri}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Extracts a runtime package into the requested extraction root when needed.
    /// </summary>
    /// <param name="packageBytes">Package bytes.</param>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="error">Extraction error details.</param>
    /// <returns>True when the package is extracted and ready.</returns>
    private static bool TryEnsureExtractedServiceRuntimePackage(byte[] packageBytes, string extractionRoot, out string error)
    {
        error = string.Empty;
        var manifestPath = Path.Combine(extractionRoot, RuntimePackageManifestFileName);
        var extractionCompleteMarkerPath = Path.Combine(extractionRoot, RuntimePackageExtractionCompleteMarkerFileName);

        // Prefer the explicit extraction-complete marker, but always validate the extracted payload
        // structure so interrupted/partial extractions cannot be treated as complete.
        if (File.Exists(extractionCompleteMarkerPath)
            && TryValidateExtractedServiceRuntimePackagePayload(extractionRoot, manifestPath, out _))
        {
            return true;
        }

        // Backfill the completion marker for valid legacy extractions that predate marker creation.
        if (TryValidateExtractedServiceRuntimePackagePayload(extractionRoot, manifestPath, out _))
        {
            TryWriteRuntimePackageExtractionMarker(extractionCompleteMarkerPath);
            return true;
        }

        try
        {
            if (Directory.Exists(extractionRoot))
            {
                TryDeleteDirectoryWithRetry(extractionRoot, maxAttempts: 5, initialDelayMs: 50);
            }

            _ = Directory.CreateDirectory(extractionRoot);
            var temporaryPackagePath = Path.Combine(extractionRoot, $"payload{RuntimePackageExtension}");
            File.WriteAllBytes(temporaryPackagePath, packageBytes);
            try
            {
                if (!TryExtractZipArchiveSafely(temporaryPackagePath, extractionRoot, out error))
                {
                    return false;
                }

                if (!TryValidateExtractedServiceRuntimePackagePayload(extractionRoot, manifestPath, out error))
                {
                    return false;
                }

                TryWriteRuntimePackageExtractionMarker(extractionCompleteMarkerPath);
                return true;
            }
            finally
            {
                TryDeleteFileQuietly(temporaryPackagePath);
            }
        }
        catch (Exception ex)
        {
            error = $"Failed to extract runtime package into '{extractionRoot}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Validates the required structure of an extracted runtime package payload.
    /// </summary>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="manifestPath">Runtime package manifest path.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when the extracted payload is structurally complete.</returns>
    private static bool TryValidateExtractedServiceRuntimePackagePayload(string extractionRoot, string manifestPath, out string error)
    {
        if (!TryValidateExtractedRuntimePackagePreconditions(extractionRoot, manifestPath, out error))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
            var root = document.RootElement;

            return TryResolveRuntimePackageHostPath(extractionRoot, manifestPath, root, out var hostPath, out error)
                && TryValidateRuntimePackageHostPath(extractionRoot, hostPath, out error)
                && TryValidateRuntimePackageModulesPath(extractionRoot, manifestPath, root, out error);
        }
        catch (Exception ex)
        {
            error = $"Failed to validate runtime package extraction at '{extractionRoot}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Validates that extraction root and runtime manifest are present before manifest inspection.
    /// </summary>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="manifestPath">Runtime package manifest path.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when required files and directories exist.</returns>
    private static bool TryValidateExtractedRuntimePackagePreconditions(string extractionRoot, string manifestPath, out string error)
    {
        error = string.Empty;
        if (!Directory.Exists(extractionRoot))
        {
            error = $"Runtime extraction root '{extractionRoot}' does not exist.";
            return false;
        }

        if (!File.Exists(manifestPath))
        {
            error = $"Runtime package manifest '{manifestPath}' was not found in extraction root '{extractionRoot}'.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that the runtime host executable resolved from manifest metadata exists.
    /// </summary>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="hostPath">Resolved host executable path.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when the host executable exists.</returns>
    private static bool TryValidateRuntimePackageHostPath(string extractionRoot, string hostPath, out string error)
    {
        error = string.Empty;
        if (File.Exists(hostPath))
        {
            return true;
        }

        error = $"Runtime package host executable '{hostPath}' was not found in extraction root '{extractionRoot}'.";
        return false;
    }

    /// <summary>
    /// Validates the optional modules payload location declared in the runtime manifest.
    /// </summary>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="manifestPath">Runtime package manifest path.</param>
    /// <param name="manifestRoot">Runtime manifest root element.</param>
    /// <param name="error">Validation error details.</param>
    /// <returns>True when modulesPath is absent, empty, or resolves to an existing directory.</returns>
    private static bool TryValidateRuntimePackageModulesPath(string extractionRoot, string manifestPath, JsonElement manifestRoot, out string error)
    {
        error = string.Empty;
        if (!manifestRoot.TryGetProperty("modulesPath", out var modulesPathProperty))
        {
            return true;
        }

        var modulesPath = modulesPathProperty.GetString();
        if (string.IsNullOrWhiteSpace(modulesPath))
        {
            return true;
        }

        if (!TryResolveRuntimeManifestPayloadPath(
                extractionRoot,
                manifestPath,
                "modulesPath",
                modulesPath,
                out var resolvedModulesPath,
                out error))
        {
            return false;
        }

        if (Directory.Exists(resolvedModulesPath))
        {
            return true;
        }

        error = $"Runtime package modules directory '{resolvedModulesPath}' was not found in extraction root '{extractionRoot}'.";
        return false;
    }

    /// <summary>
    /// Resolves the expected service host executable path from the runtime manifest.
    /// </summary>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="manifestPath">Manifest file path.</param>
    /// <param name="manifestRoot">Runtime manifest root element.</param>
    /// <param name="hostPath">Resolved host executable path.</param>
    /// <param name="error">Manifest validation error details.</param>
    /// <returns>True when host path resolves inside extraction root.</returns>
    private static bool TryResolveRuntimePackageHostPath(
        string extractionRoot,
        string manifestPath,
        JsonElement manifestRoot,
        out string hostPath,
        out string error)
    {
        error = string.Empty;
        if (manifestRoot.TryGetProperty("entryPoint", out var entryPointProperty))
        {
            var entryPoint = entryPointProperty.GetString();
            if (!string.IsNullOrWhiteSpace(entryPoint))
            {
                return TryResolveRuntimeManifestPayloadPath(
                    extractionRoot,
                    manifestPath,
                    "entryPoint",
                    entryPoint,
                    out hostPath,
                    out error);
            }
        }

        var hostBinaryName = OperatingSystem.IsWindows() ? "kestrun-service-host.exe" : "kestrun-service-host";
        hostPath = Path.Combine(extractionRoot, "host", hostBinaryName);
        return true;
    }

    /// <summary>
    /// Resolves a runtime manifest payload-relative path and validates that it remains within the extraction root.
    /// </summary>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="manifestPath">Manifest file path.</param>
    /// <param name="propertyName">Manifest property name.</param>
    /// <param name="propertyValue">Manifest property value.</param>
    /// <param name="resolvedPath">Resolved absolute payload path.</param>
    /// <param name="error">Manifest validation error details.</param>
    /// <returns>True when the resolved path is anchored under extraction root.</returns>
    private static bool TryResolveRuntimeManifestPayloadPath(
        string extractionRoot,
        string manifestPath,
        string propertyName,
        string propertyValue,
        out string resolvedPath,
        out string error)
    {
        resolvedPath = string.Empty;
        error = string.Empty;

        var normalizedExtractionRoot = Path.GetFullPath(extractionRoot);
        var extractionRootWithSeparator = normalizedExtractionRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedExtractionRoot
            : normalizedExtractionRoot + Path.DirectorySeparatorChar;

        var candidatePath = Path.GetFullPath(Path.Combine(
            normalizedExtractionRoot,
            propertyValue.Replace('/', Path.DirectorySeparatorChar)));

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!candidatePath.StartsWith(extractionRootWithSeparator, comparison))
        {
            error = $"Runtime manifest '{manifestPath}' property '{propertyName}' resolves outside extraction root '{normalizedExtractionRoot}'.";
            return false;
        }

        resolvedPath = candidatePath;
        return true;
    }

    /// <summary>
    /// Writes an extraction-complete marker file for extracted runtime payloads.
    /// </summary>
    /// <param name="markerPath">Marker file path.</param>
    private static void TryWriteRuntimePackageExtractionMarker(string markerPath)
    {
        try
        {
            File.WriteAllText(markerPath, "ok", Encoding.UTF8);
        }
        catch
        {
            // Marker creation is opportunistic; payload validation remains authoritative.
        }
    }

    /// <summary>
    /// Resolves the service host and modules layout from an extracted runtime package.
    /// </summary>
    /// <param name="rid">Runtime identifier.</param>
    /// <param name="packageId">Package id.</param>
    /// <param name="packageVersion">Package version.</param>
    /// <param name="packagePath">Package file path.</param>
    /// <param name="extractionRoot">Extraction root path.</param>
    /// <param name="requireModules">True when bundled modules are required.</param>
    /// <param name="runtimePackageLayout">Resolved runtime payload layout.</param>
    /// <param name="error">Resolution error details.</param>
    /// <returns>True when the extracted layout is valid.</returns>
    private static bool TryResolveExtractedServiceRuntimePackageLayout(
        string rid,
        string packageId,
        string packageVersion,
        string packagePath,
        string extractionRoot,
        bool requireModules,
        out ResolvedServiceRuntimePackage runtimePackageLayout,
        out string error)
    {
        runtimePackageLayout = default!;
        error = string.Empty;

        var hostBinaryName = OperatingSystem.IsWindows() ? "kestrun-service-host.exe" : "kestrun-service-host";
        var serviceHostExecutablePath = Path.Combine(extractionRoot, "host", hostBinaryName);
        var modulesPath = Path.Combine(extractionRoot, "modules");
        var manifestPath = Path.Combine(extractionRoot, RuntimePackageManifestFileName);

        if (File.Exists(manifestPath))
        {
            if (!TryReadRuntimePackageManifest(manifestPath, rid, ref serviceHostExecutablePath, ref modulesPath, out error))
            {
                return false;
            }
        }

        if (!File.Exists(serviceHostExecutablePath))
        {
            error = $"Runtime package '{packagePath}' did not contain the service host at '{serviceHostExecutablePath}'.";
            return false;
        }

        if (requireModules && !Directory.Exists(modulesPath))
        {
            error = $"Runtime package '{packagePath}' did not contain bundled modules at '{modulesPath}'.";
            return false;
        }

        runtimePackageLayout = new ResolvedServiceRuntimePackage(
            rid,
            packageId,
            packageVersion,
            packagePath,
            extractionRoot,
            Path.GetFullPath(serviceHostExecutablePath),
            requireModules ? Path.GetFullPath(modulesPath) : string.Empty);
        return true;
    }

    /// <summary>
    /// Reads runtime package manifest metadata and resolves payload-relative paths.
    /// </summary>
    /// <param name="manifestPath">Manifest file path.</param>
    /// <param name="expectedRid">Expected runtime identifier.</param>
    /// <param name="serviceHostExecutablePath">Resolved service host path.</param>
    /// <param name="modulesPath">Resolved modules path.</param>
    /// <param name="error">Manifest validation error details.</param>
    /// <returns>True when the manifest is valid.</returns>
    private static bool TryReadRuntimePackageManifest(
        string manifestPath,
        string expectedRid,
        ref string serviceHostExecutablePath,
        ref string modulesPath,
        out string error)
    {
        error = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
            var root = document.RootElement;
            if (root.TryGetProperty("rid", out var ridProperty))
            {
                var manifestRid = ridProperty.GetString();
                if (!string.Equals(manifestRid, expectedRid, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Runtime manifest '{manifestPath}' targets RID '{manifestRid}', but '{expectedRid}' was expected.";
                    return false;
                }
            }

            var extractionRoot = Path.GetDirectoryName(manifestPath) ?? string.Empty;
            if (root.TryGetProperty("entryPoint", out var entryPointProperty))
            {
                var entryPoint = entryPointProperty.GetString();
                if (!string.IsNullOrWhiteSpace(entryPoint))
                {
                    if (!TryResolveRuntimeManifestPayloadPath(
                            extractionRoot,
                            manifestPath,
                            "entryPoint",
                            entryPoint,
                            out var resolvedHostPath,
                            out error))
                    {
                        return false;
                    }

                    serviceHostExecutablePath = resolvedHostPath;
                }
            }

            if (root.TryGetProperty("modulesPath", out var modulesPathProperty))
            {
                var relativeModulesPath = modulesPathProperty.GetString();
                if (!string.IsNullOrWhiteSpace(relativeModulesPath))
                {
                    if (!TryResolveRuntimeManifestPayloadPath(
                            extractionRoot,
                            manifestPath,
                            "modulesPath",
                            relativeModulesPath,
                            out var resolvedModulesPath,
                            out error))
                    {
                        return false;
                    }

                    modulesPath = resolvedModulesPath;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Failed to read runtime manifest '{manifestPath}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Reads the package id and version from a nupkg payload.
    /// </summary>
    /// <param name="packageBytes">Package bytes.</param>
    /// <param name="packageId">Resolved package id.</param>
    /// <param name="packageVersion">Resolved package version.</param>
    /// <returns>True when package identity metadata is available.</returns>
    private static bool TryReadPackageIdentity(byte[] packageBytes, out string packageId, out string packageVersion)
    {
        packageId = string.Empty;
        packageVersion = string.Empty;

        using var stream = new MemoryStream(packageBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var nuspecEntry = archive.Entries.FirstOrDefault(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry is null)
        {
            return false;
        }

        using var reader = new StreamReader(nuspecEntry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var nuspecText = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(nuspecText))
        {
            return false;
        }

        var document = XDocument.Parse(nuspecText);
        var idElement = document.Descendants()
            .FirstOrDefault(static element => string.Equals(element.Name.LocalName, "id", StringComparison.OrdinalIgnoreCase));
        var versionElement = document.Descendants()
            .FirstOrDefault(static element => string.Equals(element.Name.LocalName, "version", StringComparison.OrdinalIgnoreCase));
        if (idElement is null || versionElement is null)
        {
            return false;
        }

        packageId = idElement.Value.Trim();
        packageVersion = versionElement.Value.Trim();
        return !string.IsNullOrWhiteSpace(packageId) && !string.IsNullOrWhiteSpace(packageVersion);
    }

    /// <summary>
    /// Converts an arbitrary token into a cache-safe path segment.
    /// </summary>
    /// <param name="value">Raw value.</param>
    /// <returns>Cache-safe token.</returns>
    private static string SanitizePathToken(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            _ = builder.Append(invalidCharacters.Contains(character) ? '-' : character);
        }

        return builder.ToString();
    }
}

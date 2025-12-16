using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Kestrun.Languages;
using Kestrun.Scripting;
using Serilog.Events;
using Kestrun.Hosting;

namespace Kestrun.Health;

/// <summary>
/// A health probe implemented via a PowerShell script.
/// </summary>
/// <param name="host">The Kestrun host instance.</param>
/// <param name="name">The name of the probe.</param>
/// <param name="tags">The tags associated with the probe.</param>
/// <param name="script">The PowerShell script to execute.</param>
/// <param name="poolAccessor">A function to access the runspace pool.</param>
/// <param name="arguments">The arguments for the script.</param>
internal sealed class PowerShellScriptProbe(
        KestrunHost host,
        string name,
        IEnumerable<string>? tags,
        string script,
        Func<KestrunRunspacePoolManager> poolAccessor,
        IReadOnlyDictionary<string, object?>? arguments) : Probe(name, tags), IProbe
{
    private Serilog.ILogger Logger => host.Logger;
    private readonly Func<KestrunRunspacePoolManager> _poolAccessor = poolAccessor ?? throw new ArgumentNullException(nameof(poolAccessor));
    private readonly IReadOnlyDictionary<string, object?>? _arguments = arguments;
    private readonly string _script = string.IsNullOrWhiteSpace(script)
        ? throw new ArgumentException("Probe script cannot be null or whitespace.", nameof(script))
        : script;

    /// <inheritdoc/>
    public override async Task<ProbeResult> CheckAsync(CancellationToken ct = default)
    {
        var pool = _poolAccessor();
        Runspace? runspace = null;
        try
        {
            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("PowerShellScriptProbe {Probe} acquiring runspace", Name);
            }
            runspace = await pool.AcquireAsync(ct).ConfigureAwait(false);
            using var ps = CreateConfiguredPowerShell(runspace);
            var output = await InvokeScriptAsync(ps, ct).ConfigureAwait(false);
            return ProcessOutput(ps, output);
        }
        catch (PipelineStoppedException) when (ct.IsCancellationRequested)
        {
            Logger.Information("PowerShell health probe {Probe} canceled (PipelineStopped).", Name);
            return new ProbeResult(ProbeStatus.Degraded, "Canceled");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex is PipelineStoppedException)
            {
                Logger.Warning(ex, "PowerShell health probe {Probe} pipeline stopped.", Name);
                return new ProbeResult(ProbeStatus.Degraded, "Canceled");
            }
            Logger.Error(ex, "PowerShell health probe {Probe} failed.", Name);
            return new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
        }
        finally
        {
            if (runspace is not null)
            {
                try { pool.Release(runspace); }
                catch { runspace.Dispose(); }
            }
        }
    }

    /// <summary>
    /// Creates and configures a PowerShell instance with the provided runspace and script.
    /// </summary>
    /// <param name="runspace">The runspace to use for the PowerShell instance.</param>
    /// <returns>A configured PowerShell instance.</returns>
    private PowerShell CreateConfiguredPowerShell(Runspace runspace)
    {
        var ps = PowerShell.Create();
        ps.Runspace = runspace;
        PowerShellExecutionHelpers.SetVariables(ps, _arguments, Logger);
        PowerShellExecutionHelpers.AddScript(ps, _script);
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("PowerShellScriptProbe {Probe} invoking script length={Length}", Name, _script.Length);
        }
        return ps;
    }

    /// <summary>
    /// Invokes the PowerShell script asynchronously.
    /// </summary>
    /// <param name="ps">The PowerShell instance to use.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, with a list of PSObject as the result.</returns>
    private async Task<IReadOnlyList<PSObject>> InvokeScriptAsync(PowerShell ps, CancellationToken ct)
    {
        var output = await ps.InvokeAsync(Logger, ct).ConfigureAwait(false);
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("PowerShellScriptProbe {Probe} received {Count} output objects", Name, output.Count);
        }
        // Materialize to a List to satisfy IReadOnlyList contract and avoid invalid casts.
        return output.Count == 0 ? Array.Empty<PSObject>() : new List<PSObject>(output);
    }

    /// <summary>
    /// Processes the output from the PowerShell script.
    /// </summary>
    /// <param name="ps">The PowerShell instance used to invoke the script.</param>
    /// <param name="output">The output objects returned by the script.</param>
    /// <returns>A ProbeResult representing the outcome of the script execution.</returns>
    private ProbeResult ProcessOutput(PowerShell ps, IReadOnlyList<PSObject> output)
    {
        if (ps.HadErrors || ps.Streams.Error.Count > 0)
        {
            var errors = string.Join("; ", ps.Streams.Error.Select(static e => e.ToString()));
            ps.Streams.Error.Clear();
            return new ProbeResult(ProbeStatus.Unhealthy, errors);
        }

        for (var i = output.Count - 1; i >= 0; i--)
        {
            if (TryConvert(output[i], out var result))
            {
                if (Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Logger.Debug("PowerShellScriptProbe {Probe} converted output index={Index} status={Status}", Name, i, result.Status);
                }
                return result;
            }
        }
        return new ProbeResult(ProbeStatus.Unhealthy, "PowerShell probe produced no recognizable result.");
    }

    /// <summary>
    /// Tries to convert a PSObject to a ProbeResult.
    /// </summary>
    /// <param name="obj">The PSObject to convert.</param>
    /// <param name="result">The resulting ProbeResult.</param>
    /// <returns>True if the conversion was successful, false otherwise.</returns>
    private static bool TryConvert(PSObject obj, out ProbeResult result)
    {
        // Direct pass-through if already a ProbeResult
        if (TryUnwrapProbeResult(obj, out result))
        {
            return true;
        }

        // Try string-based conversion
        if (TryConvertFromString(obj, out result))
        {
            return true;
        }

        // Try property-based conversion
        if (TryConvertFromProperties(obj, out result))
        {
            return true;
        }

        result = default!;
        return false;
    }

    /// <summary>
    /// Handles string-based conversion scenario
    /// </summary>
    /// <param name="obj">The PSObject to convert.</param>
    /// <param name="result">The resulting ProbeResult.</param>
    /// <returns>True if the conversion was successful, false otherwise.</returns>
    private static bool TryConvertFromString(PSObject obj, out ProbeResult result)
    {
        if (TryGetStatus(obj, out var status, out var descriptionWhenString, out var statusTextIsRaw))
        {
            if (statusTextIsRaw)
            {
                // We interpreted the entire string object as both status + description
                result = new ProbeResult(status, descriptionWhenString);
                return true;
            }
        }
        result = default!;
        return false;
    }

    /// <summary>
    /// Handles property-based conversion scenario
    /// </summary>
    /// <param name="obj">The PSObject to convert.</param>
    /// <param name="result">The resulting ProbeResult.</param>
    /// <returns>True if the conversion was successful, false otherwise.</returns>
    private static bool TryConvertFromProperties(PSObject obj, out ProbeResult result)
    {
        if (TryGetStatus(obj, out var status, out var descriptionWhenString, out var statusTextIsRaw))
        {
            if (!statusTextIsRaw)
            {
                var description = descriptionWhenString ?? GetDescription(obj);
                var data = GetDataDictionary(obj);
                result = new ProbeResult(status, description, data);
                return true;
            }
        }
        result = default!;
        return false;
    }
    /// <summary>
    /// Tries to unwrap a PSObject that directly wraps a ProbeResult.
    /// </summary>
    /// <param name="obj">The PSObject to unwrap.</param>
    /// <param name="result">The unwrapped ProbeResult.</param>
    /// <returns>True if the unwrapping was successful, false otherwise.</returns>
    private static bool TryUnwrapProbeResult(PSObject obj, out ProbeResult result)
    {
        if (obj.BaseObject is ProbeResult pr)
        {
            result = pr;
            return true;
        }
        result = default!;
        return false;
    }

    /// <summary>
    /// Parses the status from a PSObject.
    /// </summary>
    /// <param name="obj">The PSObject to parse.</param>
    /// <param name="status">The parsed ProbeStatus.</param>
    /// <param name="descriptionOrRaw">The description or raw status string.</param>
    /// <param name="isRawString">True if the status was a raw string, false otherwise.</param>
    /// <returns>True if the parsing was successful, false otherwise.</returns>
    private static bool TryGetStatus(PSObject obj, out ProbeStatus status, out string? descriptionOrRaw, out bool isRawString)
    {
        var statusValue = obj.Properties["status"]?.Value ?? obj.Properties["Status"]?.Value;
        if (statusValue is null)
        {
            if (obj.BaseObject is string statusText && TryParseStatus(statusText, out var parsedFromText))
            {
                status = parsedFromText;
                descriptionOrRaw = statusText;
                isRawString = true;
                return true;
            }
            status = default;
            descriptionOrRaw = null;
            isRawString = false;
            return false;
        }

        status = TryParseStatus(statusValue.ToString(), out var parsed)
            ? parsed
            : ProbeStatus.Unhealthy;
        descriptionOrRaw = null; // description will be resolved separately
        isRawString = false;
        return true;
    }
    /// <summary>
    /// Gets the description from a PSObject.
    /// </summary>
    /// <param name="obj">The PSObject to extract the description from.</param>
    /// <returns>The description string, or null if not found.</returns>
    private static string? GetDescription(PSObject obj)
        => obj.Properties["description"]?.Value?.ToString() ?? obj.Properties["Description"]?.Value?.ToString();

    /// <summary>
    /// Gets the data dictionary from a PSObject.
    /// </summary>
    /// <param name="obj">The PSObject to extract the data from.</param>
    /// <returns>The data dictionary, or null if not found or empty.</returns>
    private static Dictionary<string, object>? GetDataDictionary(PSObject obj)
    {
        // Extract underlying dictionary (case-insensitive property name handling already done)
        var dictionary = TryExtractDataDictionary(obj);
        if (dictionary is null)
        {
            return null;
        }

        // Build normalized temporary dictionary (allows null filtering before final allocation)
        var temp = BuildNormalizedData(dictionary);
        if (temp.Count == 0)
        {
            return null; // No meaningful data left after normalization
        }

        return PromoteData(temp);
    }

    private static IDictionary? TryExtractDataDictionary(PSObject obj)
    {
        var dataProperty = obj.Properties["data"] ?? obj.Properties["Data"];
        return dataProperty?.Value is IDictionary dict && dict.Count > 0 ? dict : null;
    }

    private static bool IsValidDataKey(string? key) => !string.IsNullOrWhiteSpace(key);

    /// <summary>
    /// Builds a normalized data dictionary from the original dictionary.
    /// </summary>
    /// <param name="dictionary">The original dictionary to normalize.</param>
    /// <returns>A new dictionary with normalized data.</returns>
    private static Dictionary<string, object?> BuildNormalizedData(IDictionary dictionary)
    {
        var temp = new Dictionary<string, object?>(dictionary.Count, StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is null || entry.Value is null)
            {
                continue;
            }
            var key = entry.Key.ToString();
            if (!IsValidDataKey(key))
            {
                continue;
            }
            var normalized = NormalizePsValue(entry.Value);
            if (normalized is not null)
            {
                temp[key!] = normalized; // key validated
            }
        }
        return temp;
    }

    /// <summary>
    /// Promotes the data from a temporary dictionary to a final dictionary.
    /// </summary>
    /// <param name="temp">The temporary dictionary to promote.</param>
    /// <returns>The promoted dictionary.</returns>
    private static Dictionary<string, object> PromoteData(Dictionary<string, object?> temp)
    {
        var final = new Dictionary<string, object>(temp.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in temp)
        {
            // Value cannot be null due to earlier filter
            final[kvp.Key] = kvp.Value!;
        }
        return final;
    }

    /// <summary>
    /// Normalizes a value that may originate from PowerShell so JSON serialization stays lean.
    /// (Delegates to smaller helpers to keep cyclomatic complexity low.)
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <param name="depth">The current recursion depth.</param>
    /// <returns>The normalized value.</returns>
    private static object? NormalizePsValue(object? value, int depth = 0)
    {
        if (value is null)
        {
            return null;
        }
        if (depth > 8)
        {
            return CollapseAtDepth(value);
        }
        if (value is PSObject psObj)
        {
            return NormalizePsPsObject(psObj, depth);
        }
        if (IsPrimitive(value))
        {
            return value;
        }
        if (value is IDictionary dict)
        {
            return NormalizeDictionary(dict, depth);
        }
        // Avoid treating strings as IEnumerable<char>
        return value switch
        {
            string s => s,
            IEnumerable seq => NormalizeEnumerable(seq, depth),
            _ => value
        };
    }

    /// <summary>
    /// Determines if a value is a primitive type that can be directly serialized.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is a primitive type, false otherwise.</returns>
    private static bool IsPrimitive(object value) => value is IFormattable && value.GetType().IsPrimitive;

    /// <summary>
    /// Collapses a value to its string representation when maximum depth is reached.
    /// </summary>
    /// <param name="value">The value to collapse.</param>
    /// <returns>The string representation of the value.</returns>
    private static string CollapseAtDepth(object value) => value is PSObject pso ? pso.ToString() : value.ToString() ?? string.Empty;

    /// <summary>
    /// Normalizes a PowerShell PSObject by extracting its base object and normalizing it.
    /// </summary>
    /// <param name="psObj">The PSObject to normalize.</param>
    /// <param name="depth">The current recursion depth.</param>
    private static object? NormalizePsPsObject(PSObject psObj, int depth)
    {
        var baseObj = psObj.BaseObject;
        return baseObj is null || ReferenceEquals(baseObj, psObj)
            ? psObj.ToString()
            : NormalizePsValue(baseObj, depth + 1);
    }

    /// <summary>
    /// Normalizes a dictionary by converting its keys to strings and recursively normalizing its values.
    /// </summary>
    /// <param name="rawDict">The raw dictionary to normalize.</param>
    /// <param name="depth">The current recursion depth.</param>
    /// <returns>A normalized dictionary with string keys and normalized values.</returns>
    private static Dictionary<string, object?> NormalizeDictionary(IDictionary rawDict, int depth)
    {
        var result = new Dictionary<string, object?>(rawDict.Count);
        foreach (DictionaryEntry de in rawDict)
        {
            if (de.Key is null)
            {
                continue;
            }
            var k = de.Key.ToString();
            if (string.IsNullOrWhiteSpace(k))
            {
                continue;
            }
            result[k] = NormalizePsValue(de.Value, depth + 1);
        }
        return result;
    }

    /// <summary>
    /// Normalizes an enumerable by recursively normalizing its items.
    /// </summary>
    /// <param name="enumerable">The enumerable to normalize.</param>
    /// <param name="depth">The current recursion depth.</param>
    /// <returns>A list of normalized items.</returns>
    private static List<object?> NormalizeEnumerable(IEnumerable enumerable, int depth)
    {
        var list = new List<object?>();
        foreach (var item in enumerable)
        {
            list.Add(NormalizePsValue(item, depth + 1));
        }
        return list;
    }

    /// <summary>
    /// Parses a status string into a ProbeStatus enum.
    /// </summary>
    /// <param name="value">The status string to parse.</param>
    /// <param name="status">The resulting ProbeStatus enum.</param>
    /// <returns>True if the parsing was successful, false otherwise.</returns>
    private static bool TryParseStatus(string? value, out ProbeStatus status)
    {
        if (Enum.TryParse(value, ignoreCase: true, out status))
        {
            return true;
        }


        switch (value?.ToLowerInvariant())
        {
            case ProbeStatusLabels.STATUS_OK:
            case ProbeStatusLabels.STATUS_HEALTHY:
                status = ProbeStatus.Healthy;
                return true;
            case ProbeStatusLabels.STATUS_WARN:
            case ProbeStatusLabels.STATUS_WARNING:
            case ProbeStatusLabels.STATUS_DEGRADED:
                status = ProbeStatus.Degraded;
                return true;
            case ProbeStatusLabels.STATUS_FAIL:
            case ProbeStatusLabels.STATUS_FAILED:
            case ProbeStatusLabels.STATUS_UNHEALTHY:
                status = ProbeStatus.Unhealthy;
                return true;
            default:
                status = ProbeStatus.Unhealthy;
                return false;
        }
    }
}

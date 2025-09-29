using System.Collections.Concurrent;
using System.Diagnostics;
using SerilogLogger = Serilog.ILogger;

namespace Kestrun.Health;

/// <summary>
/// Executes registered <see cref="IProbe"/> instances and aggregates their results into a single report.
/// </summary>
internal static class HealthProbeRunner
{
    /// <summary>
    /// Executes the provided probes and builds an aggregated <see cref="HealthReport"/>.
    /// </summary>
    /// <param name="probes">The probes to execute.</param>
    /// <param name="tagFilter">Optional tag filter (case-insensitive). When provided, only probes that advertise at least one matching tag are executed.</param>
    /// <param name="perProbeTimeout">Maximum execution time per probe. Specify <see cref="TimeSpan.Zero"/> to disable the timeout.</param>
    /// <param name="maxDegreeOfParallelism">Maximum number of probes executed concurrently. Values less than one disable throttling.</param>
    /// <param name="logger">Logger used for diagnostics.</param>
    /// <param name="ct">Cancellation token tied to the HTTP request.</param>
    /// <returns>The aggregated health report.</returns>
    public static async Task<HealthReport> RunAsync(
        IReadOnlyList<IProbe> probes,
        IReadOnlyList<string> tagFilter,
        TimeSpan perProbeTimeout,
        int maxDegreeOfParallelism,
        SerilogLogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(tagFilter);
        ArgumentNullException.ThrowIfNull(logger);

        string[] normalizedTags = tagFilter.Count == 0
            ? []
            : [.. tagFilter.Select(static t => t.Trim())
                           .Where(static t => !string.IsNullOrWhiteSpace(t))
                           .Distinct(StringComparer.OrdinalIgnoreCase)];

        var selected = normalizedTags.Length == 0
            ? probes
            : [.. probes.Where(p => p.Tags.Any(tag => normalizedTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))];

        if (selected.Count == 0)
        {
            return new HealthReport(
                ProbeStatus.Healthy,
                ProbeStatusLabels.STATUS_HEALTHY,
                DateTimeOffset.UtcNow,
                [],
                new HealthSummary(0, 0, 0, 0),
                normalizedTags);
        }

        var entries = new ConcurrentBag<HealthProbeEntry>();
        using var throttle = maxDegreeOfParallelism > 0 ? new SemaphoreSlim(maxDegreeOfParallelism) : null;

        var tasks = selected
            .Select(probe => ExecuteProbeAsync(probe, perProbeTimeout, throttle, logger, entries, ct))
            .ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var ordered = entries.OrderBy(static e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var summary = new HealthSummary(
            ordered.Length,
            ordered.Count(static e => e.Status == ProbeStatus.Healthy),
            ordered.Count(static e => e.Status == ProbeStatus.Degraded),
            ordered.Count(static e => e.Status == ProbeStatus.Unhealthy));

        // Determine overall status using explicit precedence: Unhealthy > Degraded > Healthy
        var overall = DetermineOverallStatus(ordered.Select(static e => e.Status));
        return new HealthReport(
            overall,
            overall.ToString().ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            ordered,
            summary,
            normalizedTags);
    }

    /// <summary>
    /// Executes a single probe with timeout and error handling, adding the result to the provided sink.
    /// </summary>
    /// <param name="probe">The probe to execute.</param>
    /// <param name="perProbeTimeout">Maximum execution time for the probe. Specify <see cref="TimeSpan.Zero"/> to disable the timeout.</param>
    /// <param name="throttle">Optional semaphore used to limit concurrency. May be null to disable throttling.</param>
    /// <param name="logger">Logger used for diagnostics.</param>
    /// <param name="sink">Concurrent sink to which the result is added.</param>
    /// <param name="ct">Cancellation token tied to the HTTP request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task ExecuteProbeAsync(
        IProbe probe,
        TimeSpan perProbeTimeout,
        SemaphoreSlim? throttle,
        SerilogLogger logger,
        ConcurrentBag<HealthProbeEntry> sink,
        CancellationToken ct)
    {
        if (throttle is not null)
        {
            await throttle.WaitAsync(ct).ConfigureAwait(false);
        }

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (perProbeTimeout > TimeSpan.Zero)
            {
                linkedCts.CancelAfter(perProbeTimeout);
            }

            var sw = Stopwatch.StartNew();
            ProbeResult? result = null;
            string? error = null;

            try
            {
                result = await probe.CheckAsync(linkedCts.Token).ConfigureAwait(false)
                      ?? new ProbeResult(ProbeStatus.Unhealthy, "Probe returned null result");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && linkedCts.IsCancellationRequested)
            {
                // Timeout Policy:
                // A per-probe timeout is considered a transient performance issue rather than a hard failure.
                // We classify these as Degraded to signal slowness / partial impairment while allowing
                // other Healthy probes to keep the overall status from immediately flipping to Unhealthy.
                // Rationale:
                //   * Many probes (HTTP, process, IO) may occasionally exceed a strict SLA due to load.
                //   * Treating every timeout as Unhealthy causes noisy flapping and obscures true faults.
                //   * Aggregation precedence still ensures multiple Degraded probes can surface an overall
                //     Degraded status, while a single critical failure (explicit Unhealthy) dominates.
                // If future scenarios require elevating timeouts to Unhealthy, this mapping can be made
                // configurable (e.g., via HealthProbeOptions). For now we keep policy simple & conservative.
                logger.Warning("Health probe {Probe} timed out after {Timeout}.", probe.Name, perProbeTimeout);
                result = new ProbeResult(ProbeStatus.Degraded, $"Timed out after {perProbeTimeout.TotalSeconds:N1}s");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Health probe {Probe} threw an exception.", probe.Name);
                error = ex.Message;
                result = new ProbeResult(ProbeStatus.Unhealthy, $"Exception: {ex.Message}");
            }
            finally
            {
                sw.Stop();
            }

            sink.Add(new HealthProbeEntry(
                probe.Name,
                probe.Tags ?? [],
                result.Status,
                result.Status.ToString().ToLowerInvariant(),
                result.Description,
                result.Data,
                sw.Elapsed,
                error));
        }
        finally
        {
            _ = throttle?.Release();
        }
    }

    /// <summary>
    /// Determines the overall health status using explicit precedence rules.
    /// Unhealthy takes precedence over all others, Degraded over Healthy.
    /// Returns Healthy if no statuses are provided.
    /// </summary>
    /// <param name="statuses">Collection of probe statuses.</param>
    /// <returns>The highest precedence status found.</returns>
    private static ProbeStatus DetermineOverallStatus(IEnumerable<ProbeStatus> statuses)
    {
        var foundAny = false;
        var foundDegraded = false;

        foreach (var status in statuses)
        {
            foundAny = true;
            if (status == ProbeStatus.Unhealthy)
            {
                return ProbeStatus.Unhealthy;
            }

            if (status == ProbeStatus.Degraded)
            {
                foundDegraded = true;
            }
        }

        return !foundAny ? ProbeStatus.Healthy : foundDegraded ? ProbeStatus.Degraded : ProbeStatus.Healthy;
    }
}

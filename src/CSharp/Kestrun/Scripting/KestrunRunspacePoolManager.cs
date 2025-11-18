using System.Collections.Concurrent;
using System.Management.Automation.Runspaces;
using Kestrun.Hosting;
using Serilog.Events;

namespace Kestrun.Scripting;

/// <summary>
/// Manages a pool of PowerShell runspaces for efficient reuse and resource control.
/// </summary>
public sealed class KestrunRunspacePoolManager : IDisposable
{
    private readonly ConcurrentBag<Runspace> _stash = [];
    private readonly InitialSessionState _iss;
    private int _count;        // total live runspaces
    private bool _disposed;

    private readonly ConcurrentDictionary<Runspace, byte> _all;

    /// <summary>
    /// KestrunHost is needed for logging, config, etc.
    /// </summary>
    public KestrunHost Host { get; private set; }

    /// <summary>
    /// Gets the minimum number of runspaces maintained in the pool.
    /// </summary>
    public int MinRunspaces { get; }
    /// <summary>
    /// Gets the maximum number of runspaces allowed in the pool.
    /// </summary>
    public int MaxRunspaces { get; }

    /// <summary>
    /// Thread‑affinity strategy for *future* runspaces.
    /// Default is <see cref="PSThreadOptions.ReuseThread"/>.
    /// </summary>
    public PSThreadOptions ThreadOptions { get; set; } = PSThreadOptions.ReuseThread;

    // ───────────────── constructor ──────────────────────────
    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunRunspacePoolManager"/> class with the specified minimum and maximum runspaces, initial session state, and thread options.
    /// </summary>
    /// <param name="host">The Kestrun host instance.</param>
    /// <param name="minRunspaces">The minimum number of runspaces to maintain in the pool.</param>
    /// <param name="maxRunspaces">The maximum number of runspaces allowed in the pool.</param>
    /// <param name="initialSessionState">The initial session state for each runspace (optional).</param>
    /// <param name="threadOptions">The thread affinity strategy for runspaces (optional).</param>
    public KestrunRunspacePoolManager(
        KestrunHost host,
        int minRunspaces,
        int maxRunspaces,
        InitialSessionState? initialSessionState = null,
        PSThreadOptions threadOptions = PSThreadOptions.ReuseThread)
    {
        if (host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            host.Logger.Debug("Initializing RunspacePoolManager: Min={Min}, Max={Max}", minRunspaces, maxRunspaces);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(minRunspaces);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentOutOfRangeException.ThrowIfNegative(maxRunspaces);
        // sanity check
        if (maxRunspaces < 1 || maxRunspaces < minRunspaces)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRunspaces));
        }
        _all = new();
        Host = host;
        MinRunspaces = minRunspaces;
        MaxRunspaces = maxRunspaces;
        _iss = initialSessionState ?? InitialSessionState.CreateDefault();
        ThreadOptions = threadOptions;

        // warm the stash
        for (var i = 0; i < minRunspaces; i++)
        {
            _stash.Add(CreateRunspace());
        }

        _count = minRunspaces;
        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("Warm-started pool with {Count} runspaces", _count);
        }
    }

    // ───────────────── public API ────────────────────────────
    /// <summary>Borrow a runspace (creates one if under the cap).</summary>
    public Runspace Acquire()
    {
        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("Acquiring runspace from pool: CurrentCount={Count}, Max={Max}", _count, MaxRunspaces);
        }

        ObjectDisposedException.ThrowIf(_disposed, nameof(KestrunRunspacePoolManager));

        if (_stash.TryTake(out var rs))
        {
            if (rs.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                Host.Logger.Warning("Runspace from stash is not opened: {State}. Discarding and acquiring a new one.", rs.RunspaceStateInfo.State);
                // If the runspace is not open, we cannot use it.
                // Discard and try again
                rs.Dispose();
                _ = Interlocked.Decrement(ref _count);
                return Acquire();
            }
            if (Host.Logger.IsEnabled(LogEventLevel.Debug))
            {
                Host.Logger.Debug("Reusing runspace from stash: StashCount={Count}", _stash.Count);
            }

            return rs;
        }
        // Need a new one?—but only if we haven’t reached max.
        if (Interlocked.Increment(ref _count) <= MaxRunspaces)
        {
            Host.Logger.Debug("Creating new runspace: TotalCount={Count}", _count);
            return CreateRunspace();
        }
        // Overshot: roll back and complain.
        _ = Interlocked.Decrement(ref _count);

        Host.Logger.Warning("Runspace limit reached: Max={Max}", MaxRunspaces);
        throw new InvalidOperationException("Run-space limit reached.");
    }

    /// <summary>
    /// Asynchronously acquires a runspace from the pool, creating a new one if under the cap, or waits until one becomes available.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for a runspace.</param>
    /// <returns>A task that represents the asynchronous operation, containing the acquired <see cref="Runspace"/>.</returns>
    public async Task<Runspace> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("Acquiring runspace (async) from pool: CurrentCount={Count}, Max={Max}", _count, MaxRunspaces);
        }

        while (true)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(KestrunRunspacePoolManager));

            if (_stash.TryTake(out var rs))
            {
                if (Host.Logger.IsEnabled(LogEventLevel.Debug))
                {
                    Host.Logger.Debug("Reusing runspace from stash (async): StashCount={Count}", _stash.Count);
                }

                return rs;
            }

            if (Interlocked.Increment(ref _count) <= MaxRunspaces)
            {
                Host.Logger.Debug("Creating new runspace (async): TotalCount={Count}", _count);
                // Runspace creation is synchronous, but we can offload to thread pool
                return await Task.Run(CreateRunspace, cancellationToken).ConfigureAwait(false);
            }
            _ = Interlocked.Decrement(ref _count);

            // Wait for a runspace to be returned
            if (Host.Logger.IsEnabled(LogEventLevel.Debug))
            {
                Host.Logger.Debug("Waiting for runspace to become available (async)");
            }

            // Use a short delay to poll for availability
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// Returns a runspace to the pool for reuse, or disposes it if the pool has been disposed.
    /// </summary>
    /// <param name="rs">The <see cref="Runspace"/> to return to the pool.</param>
    public void Release(Runspace rs)
    {
        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("Release() called: Disposed={Disposed}", _disposed);
        }

        if (_disposed)
        {
            Host.Logger.Warning("Pool disposed; disposing returned runspace");
            rs.Dispose();
            return;
        }

        try
        {
            // Put the genie back in the bottle: variables, funcs, modules…
            // This returns the runspace to the InitialSessionState baseline.
            rs.ResetRunspaceState();
        }
        catch (Exception ex)
        {
            Host.Logger.Warning(ex, "ResetRunspaceState failed; disposing runspace instead");
            try { rs.Close(); } catch { Host.Logger.Debug("Failed to close runspace during release after ResetRunspaceState failure"); }
            rs.Dispose();
            _ = Interlocked.Decrement(ref _count);
            return;
        }
        _stash.Add(rs);
        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("Runspace returned to stash: StashCount={Count}", _stash.Count);
        }
        // Note: we do not decrement _count here, as the pool size is fixed.
        // The pool will keep the runspace open for reuse.
    }


    // ───────────────── helpers ───────────────────────────────
    private Runspace CreateRunspace()
    {
        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("CreateRunspace() - creating new runspace");
        }

        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("Creating new runspace with InitialSessionState");
        }
        // Important: clone per runspace
        var iss = _iss.Clone();
        var rs = RunspaceFactory.CreateRunspace(iss);

        // Apply the chosen thread‑affinity strategy **before** opening.
        rs.ThreadOptions = ThreadOptions;
        rs.ApartmentState = ApartmentState.MTA;     // always MTA
        rs.Open();

        Host.Logger.Information("Opened new Runspace with ThreadOptions={ThreadOptions}", ThreadOptions);
        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("New runspace created: {Runspace}", rs);
        }

        _ = _all.TryAdd(rs, 0);
        return rs;
    }

    // ───────────────── cleanup ───────────────────────────────
    /// <summary>
    /// Disposes the runspace pool manager and all pooled runspaces.
    /// </summary>
    public void Dispose()
    {
        if (Host.Logger.IsEnabled(LogEventLevel.Debug))
        {
            Host.Logger.Debug("Disposing KestrunRunspacePoolManager: Disposed={Disposed}", _disposed);
        }

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Host.Logger.Information("Disposing RunspacePoolManager and all pooled runspaces");

        // Drain the stash
        while (_stash.TryTake(out var rs))
        {
            if (Host.Logger.IsEnabled(LogEventLevel.Debug))
            {
                Host.Logger.Debug("Disposing runspace: {Runspace}", rs);
            }
            try { rs.ResetRunspaceState(); } catch { Host.Logger.Debug("Failed to reset runspace state during disposal"); }
            try { rs.Close(); } catch { Host.Logger.Debug("Failed to close runspace during disposal"); }
            rs.Dispose();
            _ = _all.TryRemove(rs, out _);
            _ = Interlocked.Decrement(ref _count);
        }

        // Anything still checked out? Close them too.
        foreach (var kv in _all.Keys)
        {
            try { kv.ResetRunspaceState(); } catch { Host.Logger.Debug("Failed to reset runspace state during disposal"); }
            try { kv.Close(); } catch { Host.Logger.Debug("Failed to close runspace during disposal"); }
            kv.Dispose();
            _ = _all.TryRemove(kv, out _);
            _ = Interlocked.Decrement(ref _count);
        }
        Host.Logger.Information("RunspacePoolManager disposed");
    }
}

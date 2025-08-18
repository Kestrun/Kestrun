using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using Serilog;
using Serilog.Events;

namespace Kestrun.Scripting;

/// <summary>
/// Manages a pool of PowerShell runspaces for efficient reuse and resource control.
/// </summary>
public sealed class KestrunRunspacePoolManager : IDisposable
{
    private readonly ConcurrentBag<Runspace> _stash = new();
    private readonly InitialSessionState _iss;
    private readonly int _max;
    private int _count;        // total live runspaces
    private bool _disposed;

    /// <summary>
    /// Gets the minimum number of runspaces maintained in the pool.
    /// </summary>
    public int MinRunspaces { get; }
    /// <summary>
    /// Gets the maximum number of runspaces allowed in the pool.
    /// </summary>
    public int MaxRunspaces => _max;

    /// <summary>
    /// Thread‑affinity strategy for *future* runspaces.
    /// Default is <see cref="PSThreadOptions.ReuseThread"/>.
    /// </summary>
    public PSThreadOptions ThreadOptions { get; set; } = PSThreadOptions.ReuseThread;

    // ───────────────── constructor ──────────────────────────
    /// <summary>
    /// Initializes a new instance of the <see cref="KestrunRunspacePoolManager"/> class with the specified minimum and maximum runspaces, initial session state, and thread options.
    /// </summary>
    /// <param name="minRunspaces">The minimum number of runspaces to maintain in the pool.</param>
    /// <param name="maxRunspaces">The maximum number of runspaces allowed in the pool.</param>
    /// <param name="initialSessionState">The initial session state for each runspace (optional).</param>
    /// <param name="threadOptions">The thread affinity strategy for runspaces (optional).</param>
    public KestrunRunspacePoolManager(
        int minRunspaces,
        int maxRunspaces,
        InitialSessionState? initialSessionState = null,
        PSThreadOptions threadOptions = PSThreadOptions.ReuseThread)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Initializing RunspacePoolManager: Min={Min}, Max={Max}", minRunspaces, maxRunspaces);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(minRunspaces);
        if (maxRunspaces < 1 || maxRunspaces < minRunspaces)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRunspaces));
        }

        MinRunspaces = minRunspaces;
        _max = maxRunspaces;
        _iss = initialSessionState ?? InitialSessionState.CreateDefault();
        ThreadOptions = threadOptions;

        // warm the stash
        for (int i = 0; i < minRunspaces; i++)
        {
            _stash.Add(CreateRunspace());
        }

        _count = minRunspaces;
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Warm-started pool with {Count} runspaces", _count);
        }
    }

    // ───────────────── public API ────────────────────────────
    /// <summary>Borrow a runspace (creates one if under the cap).</summary>
    public Runspace Acquire()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Acquiring runspace from pool: CurrentCount={Count}, Max={Max}", _count, _max);
        }

        ObjectDisposedException.ThrowIf(_disposed, nameof(KestrunRunspacePoolManager));

        if (_stash.TryTake(out var rs))
        {
            if (rs.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                Log.Warning("Runspace from stash is not opened: {State}. Discarding and acquiring a new one.", rs.RunspaceStateInfo.State);
                // If the runspace is not open, we cannot use it.
                // Discard and try again
                rs.Dispose();
                Interlocked.Decrement(ref _count);
                return Acquire();
            }
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Reusing runspace from stash: StashCount={Count}", _stash.Count);
            }

            return rs;
        }
        // Need a new one?—but only if we haven’t reached max.
        if (Interlocked.Increment(ref _count) <= _max)
        {
            Log.Debug("Creating new runspace: TotalCount={Count}", _count);
            return CreateRunspace();
        }
        // Overshot: roll back and complain.
        Interlocked.Decrement(ref _count);

        Log.Warning("Runspace limit reached: Max={Max}", _max);
        throw new InvalidOperationException("Run-space limit reached.");
    }

    /// <summary>
    /// Asynchronously acquires a runspace from the pool, creating a new one if under the cap, or waits until one becomes available.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for a runspace.</param>
    /// <returns>A task that represents the asynchronous operation, containing the acquired <see cref="Runspace"/>.</returns>
    public async Task<Runspace> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Acquiring runspace (async) from pool: CurrentCount={Count}, Max={Max}", _count, _max);
        }

        while (true)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(KestrunRunspacePoolManager));
            }

            if (_stash.TryTake(out var rs))
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Reusing runspace from stash (async): StashCount={Count}", _stash.Count);
                }

                return rs;
            }

            if (Interlocked.Increment(ref _count) <= _max)
            {
                Log.Debug("Creating new runspace (async): TotalCount={Count}", _count);
                // Runspace creation is synchronous, but we can offload to thread pool
                return await Task.Run(() => CreateRunspace(), cancellationToken).ConfigureAwait(false);
            }
            Interlocked.Decrement(ref _count);

            // Wait for a runspace to be returned
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Waiting for runspace to become available (async)");
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
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Release() called: Disposed={Disposed}", _disposed);
        }

        if (_disposed)
        {
            Log.Warning("Pool disposed; disposing returned runspace");
            rs.Dispose();
            return;
        }

        _stash.Add(rs);
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Runspace returned to stash: StashCount={Count}", _stash.Count);
        }
        // Note: we do not decrement _count here, as the pool size is fixed.
        // The pool will keep the runspace open for reuse.
    }


    // ───────────────── helpers ───────────────────────────────
    private Runspace CreateRunspace()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("CreateRunspace() - creating new runspace");
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Creating new runspace with InitialSessionState: {Iss}", _iss);
        }

        var rs = RunspaceFactory.CreateRunspace(_iss);

        // Apply the chosen thread‑affinity strategy **before** opening.
        rs.ThreadOptions = ThreadOptions;
        rs.ApartmentState = ApartmentState.MTA;     // always MTA 

        rs.Open();
        Log.Information("Opened new Runspace with ThreadOptions={ThreadOptions}", ThreadOptions);
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("New runspace created: {Runspace}", rs);
        }

        return rs;
    }

    // ───────────────── cleanup ───────────────────────────────
    /// <summary>
    /// Disposes the runspace pool manager and all pooled runspaces.
    /// </summary>
    public void Dispose()
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Disposing KestrunRunspacePoolManager: Disposed={Disposed}", _disposed);
        }

        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Log.Information("Disposing RunspacePoolManager and all pooled runspaces");
        while (_stash.TryTake(out var rs))
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Disposing runspace: {Runspace}", rs);
            }

            try { rs.Close(); } catch { /* ignore */ }
            rs.Dispose();
        }
        Log.Information("RunspacePoolManager disposed");
    }
}

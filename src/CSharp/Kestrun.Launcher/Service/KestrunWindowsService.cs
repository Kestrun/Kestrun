using System.ServiceProcess;
using Kestrun.Launcher.Logging;
using Kestrun.Launcher.PowerShell;

namespace Kestrun.Launcher.Service;

internal sealed class KestrunWindowsService : ServiceBase
{
    private readonly string _root;
    private readonly string _serviceName;
    private readonly string _startupScript;
    private readonly string? _scriptArgs;
    private readonly SimpleLogger _logger;

    private CancellationTokenSource? _cancellation;
    private Thread? _workerThread;

    public KestrunWindowsService(string serviceName, string root, string startupScript, string? scriptArgs, SimpleLogger logger)
    {
        ServiceName = serviceName;
        _serviceName = serviceName;
        _root = root;
        _startupScript = startupScript;
        _scriptArgs = scriptArgs;
        _logger = logger;
        CanStop = true;
        CanShutdown = true;
    }

    protected override void OnStart(string[] args)
    {
        _logger.Info($"Service '{_serviceName}' starting.");
        _cancellation = new CancellationTokenSource();
        _workerThread = new Thread(() => RunWorker(_cancellation.Token))
        {
            IsBackground = true,
            Name = "KestrunLauncherServiceWorker"
        };
        _workerThread.Start();
    }

    private void RunWorker(CancellationToken cancellationToken)
    {
        try
        {
            var runner = new InProcPowerShellRunner(_logger);
            var exitCode = runner.ExecuteAsync(_root, _startupScript, _scriptArgs, cancellationToken).GetAwaiter().GetResult();
            _logger.Info($"PowerShell runner exited with code {exitCode}.");
        }
        catch (Exception ex)
        {
            _logger.Error("Service worker encountered an error", ex);
        }
    }

    protected override void OnStop()
    {
        _logger.Info($"Service '{_serviceName}' stopping.");
        if (_cancellation is not null && !_cancellation.IsCancellationRequested)
        {
            _cancellation.Cancel();
        }

        if (_workerThread is not null && !_workerThread.Join(TimeSpan.FromSeconds(20)))
        {
            _logger.Warn("Worker thread did not stop within the allotted time.");
        }
    }

    protected override void OnShutdown()
    {
        _logger.Warn("Service shutting down due to system shutdown.");
        OnStop();
    }
}

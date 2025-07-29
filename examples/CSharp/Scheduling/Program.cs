using System.Net;
using Kestrun;
using Kestrun.Logging;
using Kestrun.Scheduling;
using Serilog;
using Serilog.Events;
using System.Collections;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Kestrun.Utilities;
using Kestrun.SharedState;
using System.Text;

var cwd = Directory.GetCurrentDirectory();

// ───────── 1. Serilog
new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs/kestrun.log", rollingInterval: RollingInterval.Day)
    .Register("Log", setAsDefault: true);

// ───────── 2. Kestrun host
var server = new KestrunHost("Kestrun+Scheduler",cwd).



// basic Kestrel opts / listener
 ConfigureListener(
    port: 5000,
    ipAddress: IPAddress.Loopback,
    protocols: Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1
    );

// add PowerShell runtime & global counter
server.AddPowerShellRuntime().

AddScheduling(8); // 8 runspaces for the scheduler
// define global variable for visits
SharedStateStore.Set("Visits", new Hashtable { ["Count"] = 0 });
server.EnableConfiguration();
// ── 3.  define SCHEDULED JOBS  ──

// (A) pure C# heartbeat every 10 s

server.Scheduler.Schedule(
    "Native Heartbeat",
    TimeSpan.FromSeconds(10),
    async ct =>
    {
        Log.Information("💓  Heartbeat (C# [Native]) at {Now:O}", DateTimeOffset.UtcNow);
        await Task.Delay(100, ct);
    },
    runImmediately: true);

server.Scheduler.Schedule("Roslyn Heartbeat", TimeSpan.FromSeconds(15), code: """
    // C# code compiled by Roslyn
    Serilog.Log.Information("💓  Heartbeat (C# [Roslyn]) at {0:O}", DateTimeOffset.UtcNow);
""", lang: ScriptLanguage.CSharp, runImmediately: false);

server.Scheduler.Schedule("Powershell Heartbeat", TimeSpan.FromSeconds(20), code: """
    # PowerShell code runs inside the server process
    Write-KrInformationLog  -MessageTemplate "💓  Heartbeat (PowerShell) at {0:O}" -PropertyValues $([DateTimeOffset]::UtcNow)
""", lang: ScriptLanguage.PowerShell, runImmediately: false);

// (B) PowerShell inline – every minute
server.Scheduler.Schedule(
    "ps-inline",
    "0 * * * * *",                          // cron: every minute
    System.Management.Automation.ScriptBlock.Create("""
    Write-Information "[$([DateTime]::UtcNow.ToString('o'))] 🌙  Inline PS job ran."
    Write-Information "Runspace Name: $([runspace]::DefaultRunspace.Name)"
    Write-Information "$($Visits['Count']) Visits so far."
    """
));

// (C) PowerShell file – nightly at 03:00
var cleanupFile = new FileInfo(Path.Combine(cwd, "Scripts", "Cleanup.ps1"));
server.Scheduler.Schedule(
    "nightly-clean",
    "0 0 3 * * *",                          // 03:00 daily
    cleanupFile, ScriptLanguage.PowerShell);
// ─────── 4.  ROUTES  ─────────

// increment / show visits (unchanged)
server.AddMapRoute("/visit", HttpVerb.Get, """
    $Visits["Count"]++
    Write-KrTextResponse "🔢 Visits now: $($Visits['Count'])" 200
""", ScriptLanguage.PowerShell);

// JSON schedule report
server.AddNativeRoute("/schedule/report", HttpVerb.Get, async (ctx) =>
{
    var report = server.Scheduler.GetReport();
    await ctx.Response.WriteJsonResponseAsync(report, 200);
});

await server.RunUntilShutdownAsync(
    consoleEncoding: Encoding.UTF8,
    onStarted: () => Console.WriteLine("Server ready 🟢"),
    onShutdownError: ex => Console.WriteLine($"Shutdown error: {ex.Message}"

    )
);
using System;
using System.Diagnostics;
using System.Threading;

namespace Kestrun.Launcher;

public static class ProcessHelper
{
    /// <summary>
    /// Wait for a debugger to attach to the current process
    /// </summary>
    public static int WaitForDebugger()
    {
        var pid = Process.GetCurrentProcess().Id;
        Console.WriteLine($"Process PID: {pid}");
        Console.WriteLine("Waiting for debugger to attach...");

        while (!Debugger.IsAttached)
        {
            Thread.Sleep(200);
        }

        Console.WriteLine("Debugger attached, breaking now!");
        Debugger.Break();

        return pid;
    }
}

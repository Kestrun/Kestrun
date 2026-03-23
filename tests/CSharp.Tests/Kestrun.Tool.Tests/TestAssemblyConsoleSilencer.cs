using System.Runtime.CompilerServices;

namespace Kestrun.Tool.Tests;

internal static class TestAssemblyConsoleSilencer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Allow opting out while debugging noisy output locally.
        if (string.Equals(Environment.GetEnvironmentVariable("KESTRUN_TOOL_TESTS_VERBOSE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        Console.SetError(TextWriter.Synchronized(TextWriter.Null));
    }
}

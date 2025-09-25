using System;
using Kestrun.Hosting;

namespace DebugTest
{
    class Program
    {
        static void Main(string[] args)
        {
            // Test specific failing cases
            var testCases = new[]
            {
                "[::1]:5000",
                "[2001:db8::1]:8080",
                "ftp://localhost:5000",
                "localhost:5000:extra"
            };

            foreach (var testCase in testCases)
            {
                var result = KestrunHostMapExtensions.TryParseEndpointSpec(testCase, out var host, out var port, out var https);
                Console.WriteLine($"Test: '{testCase}' => Result: {result}, Host: '{host}', Port: {port}, HTTPS: {https}");
            }
        }
    }
}

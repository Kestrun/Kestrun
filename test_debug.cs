using System;
using System.Text.RegularExpressions;

public class Test
{
    public static void Main()
    {
        // Test the regex
        var specs = new[] {
            "localhost:5000",
            "[::1]:5000",
            "localhost",
            ":5000",
            "localhost:abc",
            "ftp://localhost:5000"
        };

        foreach (var spec in specs)
        {
            Console.WriteLine($"Testing: '{spec}'");

            // Test full URL parsing first
            if (Uri.TryCreate(spec, UriKind.Absolute, out var uri))
            {
                Console.WriteLine($"  URI parsed: Host={uri.Host}, Port={uri.Port}, Scheme={uri.Scheme}");
                if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                    uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"  Valid scheme");
                }
                else
                {
                    Console.WriteLine($"  Invalid scheme");
                }
                continue;
            }

            // Test regex parsing
            var m = Regex.Match(spec, @"^\[?(?<host>[^\]]+?)\]?:(?<port>\d+)$");
            Console.WriteLine($"  Regex match: {m.Success}");
            if (m.Success)
            {
                var hostPart = m.Groups["host"].Value;
                var portStr = m.Groups["port"].Value;
                Console.WriteLine($"  Host: '{hostPart}', Port: '{portStr}'");
                if (int.TryParse(portStr, out var portPart))
                {
                    Console.WriteLine($"  Port parsed: {portPart}");
                }
            }
            Console.WriteLine();
        }
    }
}

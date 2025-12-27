namespace Kestrun.Launcher.Cli;

internal sealed class ParsedArgs
{
    public ParsedArgs(string command, Dictionary<string, string?> options)
    {
        Command = command;
        Options = options;
    }

    public string Command { get; }

    public Dictionary<string, string?> Options { get; }

    public bool HasOption(string name) => Options.ContainsKey(name);

    public string? GetOption(string name)
    {
        Options.TryGetValue(name, out var value);
        return value;
    }

    public string RequireOption(string name)
    {
        var value = GetOption(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required option --{name}");
        }

        return value;
    }
}

internal static class Args
{
    public static ParsedArgs Parse(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Missing command");
        }

        var command = args[0].Trim().ToLowerInvariant();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < args.Length; index++)
        {
            var current = args[index];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{current}'");
            }

            var name = current[2..];
            string? value = null;
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[index + 1];
                index++;
            }
            else
            {
                value = "true";
            }

            options[name] = value;
        }

        return new ParsedArgs(command, options);
    }
}

using System.Globalization;
using System.Text.Json;

/// <summary>
/// Attribute to specify OpenAPI vendor extensions (x-*) on an API operation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class OpenApiExtensionAttribute : KestrunAnnotation
{
    /// <summary>
    /// The extension name. Must start with "x-".
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Raw JSON value for the extension.
    /// </summary>
    public string Json { get; }

    /// <summary>
    /// Creates an OpenAPI extension.
    /// If <paramref name="value"/> is valid JSON, it is used as-is.
    /// Otherwise, it is treated as a string literal and JSON-encoded.
    /// </summary>
    public OpenApiExtensionAttribute(string name, string value)
    {
        ValidateName(name);
        Name = name;

        if (value is null)
        {
            Json = "null";
            return;
        }

        if (IsValidJson(value))
        {
            Json = value;
        }
        else
        {
            // Treat as string literal
            Json = JsonSerializer.Serialize(value);
        }
    }

    /// <summary>
    /// Creates an OpenAPI extension with a boolean value.
    /// </summary>
    public OpenApiExtensionAttribute(string name, bool value)
    {
        ValidateName(name);
        Name = name;
        Json = value ? "true" : "false";
    }

    /// <summary>
    /// Creates an OpenAPI extension with an integer value.
    /// </summary>
    public OpenApiExtensionAttribute(string name, int value)
    {
        ValidateName(name);
        Name = name;
        Json = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Creates an OpenAPI extension with a numeric value.
    /// </summary>
    public OpenApiExtensionAttribute(string name, double value)
    {
        ValidateName(name);
        Name = name;
        Json = value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Validates the extension name.
    /// </summary>
    /// <param name="name">The extension name to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the extension name is null, empty, or does not start with "x-".</exception>
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Extension name cannot be null or empty.", nameof(name));
        }

        if (!name.StartsWith("x-", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"OpenAPI extension '{name}' is invalid. Extension names must start with 'x-'.",
                nameof(name));
        }
    }

    /// <summary>
    /// Validates whether a string is valid JSON.
    /// </summary>
    /// <param name="value"> The string to validate as JSON. </param>
    /// <returns> True if the string is valid JSON; otherwise, false. </returns>
    private static bool IsValidJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}


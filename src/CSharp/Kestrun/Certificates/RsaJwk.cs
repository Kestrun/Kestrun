
using System.Text.Json.Serialization;

namespace Kestrun.Certificates;

/// <summary>
/// Represents an RSA JSON Web Key (JWK).
/// </summary>
public sealed class RsaJwk
{
    /// <summary>
    /// Key Type - should be "RSA"
    /// </summary>
    [JsonPropertyName("kty")]
    public string Kty { get; set; } = "RSA";

    /// <summary>
    /// Modulus
    /// </summary>
    [JsonPropertyName("n")]
    public string? N { get; set; }

    /// <summary>
    /// Exponent
    /// </summary>
    [JsonPropertyName("e")]
    public string? E { get; set; }

    /// <summary>
    /// Private Exponent
    /// </summary>
    [JsonPropertyName("d")]
    public string? D { get; set; }

    /// <summary>
    /// First Prime Factor
    /// </summary>
    [JsonPropertyName("p")]
    public string? P { get; set; }

    /// <summary>
    /// Second Prime Factor
    /// </summary>
    [JsonPropertyName("q")]
    public string? Q { get; set; }

    /// <summary>
    /// First Factor CRT Exponent
    /// </summary>
    [JsonPropertyName("dp")]
    public string? DP { get; set; }

    /// <summary>
    /// Second Factor CRT Exponent
    /// </summary>
    [JsonPropertyName("dq")]
    public string? DQ { get; set; }

    /// <summary>
    /// CRT Coefficient
    /// </summary>
    [JsonPropertyName("qi")]
    public string? QI { get; set; }

    /// <summary>
    /// Key ID
    /// </summary>
    [JsonPropertyName("kid")]
    public string? Kid { get; set; }
}

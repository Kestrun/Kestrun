namespace Kestrun.Utilities.Yaml;

/// <summary>
/// Options for YAML serialization
/// </summary>
[Flags]
public enum SerializationOptions
{
    /// <summary>
    /// No special options
    /// </summary>
    None = 0,
    /// <summary>
    /// Ensure round-trip serialization/deserialization
    /// </summary>
    Roundtrip = 1,
    /// <summary>
    /// Disable YAML aliases
    /// </summary>
    DisableAliases = 2,
    /// <summary>
    /// Emit default values
    /// </summary>
    EmitDefaults = 4,
    /// <summary>
    /// Ensure JSON compatibility
    /// </summary>
    JsonCompatible = 8,
    /// <summary>
    /// Default to static type resolution
    /// </summary>
    DefaultToStaticType = 16,
    /// <summary>
    /// Use indented sequences
    /// </summary>
    WithIndentedSequences = 32,
    /// <summary>
    /// Omit null values
    /// </summary>
    OmitNullValues = 64,
    /// <summary>
    /// Use flow style
    /// </summary>
    UseFlowStyle = 128,
    /// <summary>
    /// Use sequence flow style
    /// </summary>
    UseSequenceFlowStyle = 256
}

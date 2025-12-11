/// <summary>
/// Serialization style hints for parameters (per OAS 3.x).
/// </summary>
public enum OaParameterStyle
{
    /// <summary>Default for path &amp; header (no delimiters).</summary>
    Simple = 0,
    /// <summary>Default for query &amp; cookie.</summary>
    Form,
    /// <summary>Matrix style for path; e.g., ;color=blue.</summary>
    Matrix,
    /// <summary>Label style for path; e.g., .blue.</summary>
    Label,
    /// <summary>Space-delimited arrays in query.</summary>
    SpaceDelimited,
    /// <summary>Pipe-delimited arrays in query.</summary>
    PipeDelimited,
    /// <summary>Deep object style for nested objects in query.</summary>
    DeepObject
}

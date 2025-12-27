[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class OpenApiCallbackRefAttribute : KestrunAnnotation
{
    /// <summary>
    /// The local name under callbacks
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The components/callbacks id (the ID of the callback component to reference).
    /// </summary>
    public required string ReferenceId { get; set; }

    /// <summary>
    /// If true, the callback will be inlined rather than referenced.
    /// </summary>
    public bool Inline { get; set; }
}

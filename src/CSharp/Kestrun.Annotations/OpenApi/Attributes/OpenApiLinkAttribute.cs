/// <summary>
/// Place on a property or parameter to indicate it is an OpenAPI link.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class OpenApiLinkAttribute : KestrunAnnotation
{
    /// <summary>
    /// Override the parameter name (default: property name).
    /// </summary>
    public string? Key { get; set; }
    /// <summary>
    /// Optional description for the link.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The operationId of the linked operation.
    /// </summary>
    public string? OperationId { get; set; }

    /// <summary>
    /// The operationId of the linked operation.
    /// </summary>
    public string? OperationRef { get; set; }
    /// <summary>
    /// The map key for the request parameter.
    /// </summary>
    public string? MapKey { get; set; }
    /// <summary>
    /// The map value for the request parameter.
    /// </summary>
    public string? MapValue { get; set; }
    /// <summary>
    /// The request body expression or json for the link.
    /// </summary>
    public string? RequestBodyExpression { get; set; }
    /// <summary>
    /// The request body json for the link.
    /// </summary>
    public string? RequestBodyJson { get; set; }

}

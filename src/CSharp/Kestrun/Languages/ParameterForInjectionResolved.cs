namespace Kestrun.Languages;

/// <summary>
/// Resolved parameter information for injection into a script.
/// </summary>
public class ParameterForInjectionResolved : ParameterForInjectionInfoBase
{
    /// <summary>
    /// The resolved value of the parameter.
    /// </summary>
    public object? Value { get; init; }
    /// <summary>
    /// Constructs a ResolvedRequestParameters instance from ParameterForInjectionInfo and its resolved value.
    /// </summary>
    /// <param name="paramInfo">The parameter metadata.</param>
    /// <param name="value">The resolved value of the parameter.</param>
    public ParameterForInjectionResolved(ParameterForInjectionInfo paramInfo, object? value) :
     base(paramInfo.Name, paramInfo.ParameterType)
    {
        ArgumentNullException.ThrowIfNull(paramInfo);
        Type = paramInfo.Type;
        DefaultValue = paramInfo.DefaultValue;
        In = paramInfo.In;
        Value = value;
    }
}

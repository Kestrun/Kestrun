
using System.Management.Automation;
using Kestrun.Models;
using Microsoft.Extensions.Primitives;
using Microsoft.OpenApi;

namespace Kestrun.Languages;
/// <summary>
/// Information about a parameter to be injected into a script.
/// </summary>
public record ParameterForInjectionInfo
{
    /// <summary>
    /// The name of the parameter.
    /// </summary>
    public string Name { get; init; }
    /// <summary>
    /// The JSON schema type of the parameter.
    /// </summary>
    public JsonSchemaType? Type { get; init; }
    /// <summary>
    /// The location of the parameter.
    /// </summary>
    public ParameterLocation? In { get; init; }

    /// <summary>
    /// Constructs a ParameterForInjectionInfo with the specified properties.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="type">The JSON schema type of the parameter.</param>
    /// <param name="in">The location of the parameter.</param>
    public ParameterForInjectionInfo(string name, JsonSchemaType? type, ParameterLocation? @in)
    {
        Name = name;
        Type = type;
        In = @in;
    }

    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiParameter.
    /// </summary>
    /// <param name="parameter">The OpenApiParameter to construct from.</param>
    public ParameterForInjectionInfo(OpenApiParameter? parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        Name = parameter.Name!;
        Type = parameter.Schema?.Type;
        In = parameter.In;
    }


    /// <summary>
    /// Adds parameters from the HTTP context to the PowerShell instance.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="ps">The PowerShell instance to which parameters will be added.</param>
    internal static void InjectParameters(KestrunContext context, PowerShell ps)
    {
        var parameters = context.HttpContext.GetEndpoint()?
               .Metadata
               .FirstOrDefault(m => m is List<ParameterForInjectionInfo>)
               as List<ParameterForInjectionInfo>;
        var logger = context.Host.Logger;
        if (parameters is not null)
        {
            if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            {
                logger.Debug("Injecting {Count} parameters into PowerShell script.", parameters.Count);
            }

            foreach (var param in parameters)
            {
                var name = param.Name;

                var raw = param.In switch
                {
                    ParameterLocation.Path => context.Request.RouteValues?[name],
                    ParameterLocation.Query => (object?)context.Request.Query[name],
                    ParameterLocation.Header => (object?)context.Request.Headers[name],
                    ParameterLocation.Cookie => context.Request.Cookies[name],
                    _ => null,
                };

                if (raw is null)
                {
                    _ = ps.AddParameter(name, null);
                    continue;
                }

                // Normalize all cases to string or string[]
                string? singleValue = null;
                string?[]? multiValue = null;

                switch (raw)
                {
                    case StringValues sv:
                        // if multi-valued, you may want array
                        multiValue = [.. sv];
                        singleValue = sv.Count > 0 ? sv[0] : null;
                        break;

                    case string s:
                        singleValue = s;
                        break;

                    default:
                        singleValue = raw.ToString();
                        break;
                }

                if (singleValue is null && multiValue is null)
                {
                    _ = ps.AddParameter(name, null);
                    continue;
                }

                object? converted = param.Type switch
                {
                    JsonSchemaType.Integer => int.TryParse(singleValue, out var i) ? i : null,
                    JsonSchemaType.Number => double.TryParse(singleValue, out var d) ? d : null,
                    JsonSchemaType.Boolean => bool.TryParse(singleValue, out var b) ? b : null,
                    JsonSchemaType.Array => multiValue ?? (singleValue is not null ? new[] { singleValue } : null),
                    JsonSchemaType.Object => singleValue, // unless you’re deserializing JSON here
                    JsonSchemaType.String => singleValue,
                    _ => singleValue,
                };

                _ = ps.AddParameter(name, converted);
            }
        }
    }
}

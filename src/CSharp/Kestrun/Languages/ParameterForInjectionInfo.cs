
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
    /// Indicates whether the parameter is from the request body.
    /// </summary>
    public bool IsRequestBody => In is null;

    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiParameter.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="parameter">The OpenApiParameter to construct from.</param>
    public ParameterForInjectionInfo(string name, OpenApiParameter? parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Type = parameter.Schema?.Type;
        In = parameter.In;
    }
    /// <summary>
    /// Constructs a ParameterForInjectionInfo from an OpenApiRequestBody.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="requestBody">The OpenApiRequestBody to construct from.</param>
    public ParameterForInjectionInfo(string name, OpenApiRequestBody requestBody)
    {
        ArgumentNullException.ThrowIfNull(requestBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Type = requestBody.Content?.Values.FirstOrDefault()?.Schema?.Type;
        In = null;
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
                if (logger.IsEnabled(Serilog.Events.LogEventLevel.Debug))
                {
                    logger.Debug("Injecting parameter '{Name}' of type '{Type}' from '{In}'.", name, param.Type, param.In);
                }
                // Get the raw value from the appropriate location
                var raw = param.In switch
                {
                    ParameterLocation.Path => context.Request.RouteValues?[name],
                    ParameterLocation.Query => (object?)context.Request.Query[name],
                    ParameterLocation.Header => (object?)context.Request.Headers[name],
                    ParameterLocation.Cookie => context.Request.Cookies[name],
                    null => context.Request.Body,
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

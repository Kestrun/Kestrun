using System.Management.Automation;
using System.Reflection;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.CodeAnalysis.CSharp;

namespace Kestrun.Hosting.Options;
/// <summary>
/// Options for mapping a route, including pattern, HTTP verbs, script code, authorization, and metadata.
/// </summary>
public partial class MapRouteBuilder : MapRouteOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapRouteBuilder"/> class with the specified Kestrun host.
    /// </summary>
    /// <param name="server">The Kestrun host this route is associated with.</param>
    /// <param name="pattern">The route pattern to match for this option.</param>
    public MapRouteBuilder(KestrunHost server, string? pattern)
    {
        Server = server;
        Pattern = pattern;
    }
    /// <summary>
    /// The Kestrun host this route is associated with.
    /// Used by the MapRouteBuilder cmdlet.
    /// </summary>
    public KestrunHost Server { get; init; }

    /// <summary>
    /// Returns a string representation of the MapRouteBuilder, showing HTTP verbs and pattern.
    /// </summary>
    /// <returns>A string representation of the MapRouteBuilder.</returns>
    public override string ToString()
    {
        var verbs = HttpVerbs.Count > 0 ? string.Join(",", HttpVerbs) : "ANY";
        return $"{Server?.ApplicationName} {verbs} {Pattern}";
    }

    /// <summary>
    /// Creates a new instance of the <see cref="MapRouteBuilder"/> class with the specified Kestrun host, pattern, and HTTP verbs.
    /// </summary>
    /// <param name="server"> The Kestrun host this route is associated with.</param>
    /// <param name="pattern"> The route pattern to match for this option.</param>
    /// <param name="httpVerbs">The HTTP verbs to match for this route.</param>
    /// <returns>A new instance of <see cref="MapRouteBuilder"/>.</returns>
    public static MapRouteBuilder Create(KestrunHost server, string? pattern, IEnumerable<HttpVerb> httpVerbs) => new(server, pattern)
    {
        HttpVerbs = [.. httpVerbs]
    };

    /// <summary>
    /// Adds a PowerShell script block as the script code for this route.
    /// </summary>
    /// <param name="scriptBlock"> The PowerShell script block to add as the script code.</param>
    /// <returns> A new instance of <see cref="MapRouteBuilder"/> with the added script block.</returns>
    public MapRouteBuilder AddScriptBlock(ScriptBlock scriptBlock)
    {
        ArgumentNullException.ThrowIfNull(scriptBlock);

        ScriptCode.Code = scriptBlock.Ast.Extent.Text;
        ScriptCode.Language = ScriptLanguage.PowerShell;
        return this;
    }

    /// <summary>
    /// Adds a code block in a specified scripting language to this route.
    /// </summary>
    /// <param name="codeBlock">The code block that defines the route's behavior.</param>
    /// <param name="language">The scripting language of the code block.</param>
    /// <param name="extraImports">Optional additional namespaces to import for the script.</param>
    /// <param name="extraRefs">Optional additional assemblies to reference for the script.</param>
    /// <param name="arguments">Optional arguments to pass to the script.</param>
    /// <param name="languageVersion">Optional language version for the script. Defaults to Latest.</param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddCodeBlock(
        string codeBlock,
        ScriptLanguage language,
        IEnumerable<string>? extraImports = null,
        IEnumerable<Assembly>? extraRefs = null,
        IDictionary<string, object>? arguments = null,
        LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeBlock);

        ScriptCode.Language = language;
        ScriptCode.Code = codeBlock;
        ScriptCode.LanguageVersion = languageVersion;

        // Map optional collections
        ScriptCode.ExtraImports = extraImports?.ToArray();
        ScriptCode.ExtraRefs = extraRefs?.ToArray();
        if (arguments is not null)
        {
            // Convert to Dictionary<string, object?> matching ScriptCode.Arguments nullable values
            // Cast each value to object? to satisfy Dictionary<string, object?> target type
            ScriptCode.Arguments = arguments.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
        }
        return this;
    }



    /// <summary>
    /// Adds code from a file to this route's script configuration.
    /// The script language is inferred from the file extension.
    /// </summary>
    /// <param name="codeFilePath">The file path to the code file that defines the route's behavior.</param>
    /// <param name="extraImports">Optional additional namespaces to import for the script.</param>
    /// <param name="extraRefs">Optional additional assemblies to reference for the script.</param>
    /// <param name="arguments">Optional arguments to pass to the script.</param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddCodeFromFile(
        string codeFilePath,
        IEnumerable<string>? extraImports = null,
        IEnumerable<Assembly>? extraRefs = null,
        IDictionary<string, object>? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeFilePath);

        if (!File.Exists(codeFilePath))
        {
            throw new FileNotFoundException(
                $"The specified code file path does not exist: {codeFilePath}",
                codeFilePath);
        }

        // Map optional collections
        ScriptCode.ExtraImports = extraImports?.ToArray();
        ScriptCode.ExtraRefs = extraRefs?.ToArray();

        if (arguments is not null)
        {
            ScriptCode.Arguments = arguments.ToDictionary(
                kvp => kvp.Key,
                kvp => (object?)kvp.Value);
        }

        // Infer language from extension
        var extension = Path.GetExtension(codeFilePath);

        ScriptCode.Language = extension.ToLowerInvariant() switch
        {
            ".ps1" => ScriptLanguage.PowerShell,
            ".cs" => ScriptLanguage.CSharp,
            ".vb" => ScriptLanguage.VBNet,
            _ => throw new NotSupportedException(
                                $"Unsupported '{extension}' code file extension."),
        };

        // Load file content as raw script code
        ScriptCode.Code = File.ReadAllText(codeFilePath);

        return this;
    }

#if !OPENAPI
    /// <summary>
    /// Adds authorization requirements (schemes + policies) to this route and
    /// configures corresponding OpenAPI security metadata.
    /// </summary>
    /// <param name="policies">Authorization policy names required for the route. These are also treated as scopes for the OpenAPI security requirements. </param>
    /// <param name="verbs">Optional HTTP verbs to which the authorization will be applied.If null or empty, the authorization is applied to all verbs defined in <see cref="HttpVerb"/>.</param>
    /// <param name="scheme">Optional explicit authentication scheme name to include in the security requirements.</param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddAuthorization(
        IEnumerable<string>? policies = null,
        IEnumerable<HttpVerb>? verbs = null,
        string? scheme = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);
        // Normalize inputs
        var policyList = policies?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList()
                        ?? [];

        // If no verbs passed, apply to all defined on the builder
        var effectiveVerbs = (verbs is null || !verbs.Any())
            ? HttpVerbs
            : verbs;

        // This collects all schemes used so we can add them to RequireSchemes at the end
        List<string>? allSchemes = [];

        // to remove after merge
        if (!string.IsNullOrWhiteSpace(scheme))
        {
            allSchemes.Add(scheme);
        }

        AddSecurityRequirementObject(schemes: allSchemes, policies: policyList);

        return this;
    }
#endif
}

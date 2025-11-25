using System.Management.Automation;
using System.Reflection;
using Kestrun.Authentication;
using Kestrun.OpenApi;
using Kestrun.Scripting;
using Kestrun.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.OpenApi;

namespace Kestrun.Hosting.Options;
/// <summary>
/// Options for mapping a route, including pattern, HTTP verbs, script code, authorization, and metadata.
/// </summary>
public class MapRouteBuilder : MapRouteOptions
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
    /// Adds OpenAPI tags to the route for the specified HTTP verbs.
    /// </summary>
    /// <param name="tags">The OpenAPI tags to add.</param>
    /// <param name="verbs">The HTTP verbs to which the tags should be applied. If null, tags are applied to all verbs.</param>
    /// <returns>The current instance of <see cref="MapRouteBuilder"/> with the added OpenAPI tags.</returns>
    /// <exception cref="ArgumentException">Thrown when no valid tags are provided.</exception>
    public MapRouteBuilder AddOpenApiTag(IEnumerable<string> tags, IEnumerable<HttpVerb>? verbs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);
        // If no verbs passed, apply to the builder's own verbs
        var effectiveVerbs = (verbs is null || !verbs.Any())
            ? HttpVerbs
            : verbs;

        // Validate tags
        var tagList = tags
              .Where(t => !string.IsNullOrWhiteSpace(t))
              .ToList();

        // Ensure at least one valid tag
        if (tagList.Count == 0)
        {
            throw new ArgumentException("At least one non-empty tag must be provided.", nameof(tags));
        }

        foreach (var verb in effectiveVerbs)
        {
            // Get or create OpenAPIMetadata for the verb
            if (!OpenAPI.TryGetValue(verb, out var metadata))
            {
                // Create new metadata if not present
                metadata = new OpenAPIMetadata(Pattern);
                OpenAPI[verb] = metadata;
            }
            // Ensure metadata is enabled
            metadata.Enabled = true;

            // Add tags to the metadata
            foreach (var tag in tagList)
            {
                // Avoid duplicates in tags
                if (!metadata.Tags.Contains(tag))
                {
                    metadata.Tags.Add(tag);
                }
            }
        }

        // Return the same builder instance for chaining
        return this;
    }

    /// <summary>
    /// Adds OpenAPI server information to this route.
    /// If no verbs are specified, servers are added at the path level.
    /// Otherwise, servers are added to the specified verb-level metadata.
    /// </summary>
    /// <param name="servers">The OpenAPI servers to associate with this route.</param>
    /// <param name="verbs">Optional HTTP verbs to which the servers will be applied.If null or empty, servers are applied at the path level.</param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddOpenApiServer(
        IEnumerable<OpenApiServer> servers,
        IEnumerable<HttpVerb>? verbs = null)
    {
        ArgumentNullException.ThrowIfNull(servers);
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);

        var serverList = servers.ToList();
        if (serverList.Count == 0)
        {
            throw new ArgumentException("At least one OpenAPI server must be provided.", nameof(servers));
        }

        // Determine if we are using path-level metadata (Verbs.Count -eq 0 in PS)
        if (verbs is null || !verbs.Any())
        {
            // Path-level metadata
            PathLevelOpenAPIMetadata ??= new OpenAPICommonMetadata();

            // Ensure Servers collection is initialized
            PathLevelOpenAPIMetadata.Servers ??= [];

            foreach (var s in serverList)
            {
                PathLevelOpenAPIMetadata.Servers.Add(s);
            }
        }
        else
        {
            // Verb-level metadata
            foreach (var verb in verbs)
            {
                if (!OpenAPI.TryGetValue(verb, out var metadata))
                {
                    metadata = new OpenAPIMetadata(Pattern);
                    OpenAPI[verb] = metadata;
                }

                metadata.Enabled = true;

                metadata.Servers ??= [];
                foreach (var s in serverList)
                {
                    metadata.Servers.Add(s);
                }
            }
        }
        // Return the same builder instance for chaining
        return this;
    }

    /// <summary>
    /// Adds an OpenAPI response to this route for the given HTTP verbs.
    /// If no verbs are specified, the response is applied to all verbs defined on the builder.
    /// The response can either be created inline (description only) or
    /// based on a referenced response from an OpenAPI document.
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the OpenAPI response.</param>
    /// <param name="description">
    /// Optional description of the response. Required when no <paramref name="referenceId"/> is provided.
    /// When <paramref name="referenceId"/> is provided, this overrides the description of the referenced response if set.
    /// </param>
    /// <param name="verbs">
    /// Optional HTTP verbs to which this response applies.
    /// If <c>null</c> or empty, all verbs defined on <see cref="HttpVerb"/> are used.
    /// </param>
    /// <param name="docId">
    /// The documentation ID to use when resolving a referenced response.
    /// If <c>null</c>, defaults to <see cref="IOpenApiAuthenticationOptions.DefaultSchemeName"/>.
    /// </param>
    /// <param name="referenceId">
    /// Optional reference ID of a response defined in the OpenAPI document components.
    /// When provided, the response is based on that component, optionally embedded or referenced.
    /// </param>
    /// <param name="embed">
    /// If <c>true</c> and <paramref name="referenceId"/> is provided, the component is cloned and embedded.
    /// If <c>false</c>, a reference-based response is used instead.
    /// </param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddOpenApiResponse(
        string statusCode,
        string? description,
        IEnumerable<HttpVerb>? verbs = null,
        string? docId = null,
        string? referenceId = null,
        bool embed = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statusCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);
        // Default DocId like in PowerShell:
        // [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultSchemeName
        docId ??= IOpenApiAuthenticationOptions.DefaultSchemeName;

        // Determine effective verbs (PowerShell: if ($Verbs.Count -eq 0) { $Verbs = $MapRouteBuilder.HttpVerbs })
        var effectiveVerbs = (verbs is null || !verbs.Any())
            ? HttpVerbs
            : verbs;

        foreach (var verb in effectiveVerbs)
        {
            if (!OpenAPI.TryGetValue(verb, out var metadata))
            {
                metadata = new OpenAPIMetadata(Pattern);
                OpenAPI[verb] = metadata;
            }

            IOpenApiResponse response;

            if (!string.IsNullOrEmpty(referenceId))
            {
                // Reference-based response
                var docDescriptor = Server.OpenApiDocumentDescriptor[docId];
                // Validate document and components
                if (docDescriptor.Document.Components is null)
                {
                    throw new InvalidOperationException(
                        $"The OpenAPI document with ID '{docId}' does not contain any components.");
                }
                // Validate response exists
                var responses = docDescriptor.Document.Components.Responses ?? throw new InvalidOperationException(
                        $"The OpenAPI document with ID '{docId}' does not contain any response components.");
                // Try get the referenced response
                if (!responses.TryGetValue(referenceId, out var componentResponse))
                {
                    throw new InvalidOperationException(
                        $"Response with ReferenceId '{referenceId}' does not exist in the OpenAPI document components.");
                }

                if (embed)
                {
                    // Embed: clone the component and optionally override description
                    var cloned = OpenApiComponentClone.Clone(componentResponse);
                    if (!string.IsNullOrEmpty(description))
                    {
                        cloned.Description = description;
                    }

                    response = cloned;
                }
                else
                {
                    // Reference: wrap as a response reference
                    // Mirrors: [Microsoft.OpenApi.OpenApiResponseReference]::new($ReferenceId)
                    var referenceResponse = new OpenApiResponseReference(referenceId);

                    // PowerShell still lets Description override even on reference
                    if (!string.IsNullOrEmpty(description))
                    {
                        referenceResponse.Description = description;
                    }

                    response = referenceResponse;
                }
            }
            else
            {
                // Description-only response (no ReferenceId)
                if (string.IsNullOrWhiteSpace(description))
                {
                    throw new ArgumentException(
                        "Description must be provided when no ReferenceId is specified.",
                        nameof(description));
                }

                response = new OpenApiResponse
                {
                    Description = description
                };
            }

            // Ensure the route metadata is marked as having OpenAPI data
            metadata.Enabled = true;

            // PowerShell code re-applies Description if bound:
            // if ($PSBoundParameters.ContainsKey('Description')) { $response.Description = $Description }
            if (!string.IsNullOrEmpty(description))
            {
                response.Description = description;
            }
            // Ensure Responses collection is initialized
            metadata.Responses ??= [];
            // Add/update response by status code
            metadata.Responses[statusCode] = response;
        }

        return this;
    }

    /// <summary>
    /// Convenience overload: description-only response applied to all verbs.
    /// </summary>
    public MapRouteBuilder AddOpenApiResponse(
        string statusCode,
        string description)
        => AddOpenApiResponse(statusCode, description, verbs: null, docId: null, referenceId: null, embed: false);

    /// <summary>
    /// Convenience overload: reference-based response, optionally embedded.
    /// </summary>
    public MapRouteBuilder AddOpenApiResponseFromReference(
        string statusCode,
        string referenceId,
        string? description = null,
        IEnumerable<HttpVerb>? verbs = null,
        string? docId = null,
        bool embed = false)
        => AddOpenApiResponse(statusCode, description, verbs, docId, referenceId, embed);

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
    /// Adds OpenAPI metadata such as summary, description, tags, operation ID and deprecated flag
    /// either at the path level or at the verb level.
    /// </summary>
    /// <param name="summary">Optional brief summary of the route. Applied to path-level or verb-level metadata depending on <paramref name="pathLevel"/>.</param>
    /// <param name="description">Optional detailed description of the route.</param>
    /// <param name="operationId">Optional unique operation ID. Only valid when applied to a single verb.</param>
    /// <param name="deprecated">Optional deprecated flag for the operation. Only applied at verb level.</param>
    /// <param name="verbs"> Optional verbs to which metadata will be applied.
    /// If null or empty and <paramref name="pathLevel"/> is false, all <see cref="HttpVerb"/> are used.
    /// Ignored when <paramref name="pathLevel"/> is true.</param>
    /// <param name="pathLevel">If true, applies metadata at path level; otherwise, at verb level.</param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddOpenApiInfo(
        string? summary = null,
        string? description = null,
        string? operationId = null,
        bool deprecated = false,
        IEnumerable<HttpVerb>? verbs = null,
        bool pathLevel = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);
        if (pathLevel)
        {
            // Path-level metadata
            PathLevelOpenAPIMetadata = new OpenAPICommonMetadata(Pattern);

            if (summary is not null)
            {
                PathLevelOpenAPIMetadata.Summary = summary;
            }

            if (description is not null)
            {
                PathLevelOpenAPIMetadata.Description = description;
            }

            // Tags / OperationId / Deprecated are NOT applied at path level in the PS version
            return this;
        }

        // Verb-level metadata
        var effectiveVerbs = (verbs is null || !verbs.Any())
            ? HttpVerbs
            : verbs;

        // Validate OperationId only when a single verb is targeted
        if (effectiveVerbs.Count() > 1 && !string.IsNullOrWhiteSpace(operationId))
        {
            throw new InvalidOperationException("OperationId cannot be set for multiple verbs.");
        }

        foreach (var verb in effectiveVerbs)
        {
            if (!OpenAPI.TryGetValue(verb, out var metadata))
            {
                metadata = new OpenAPIMetadata(Pattern);
                OpenAPI[verb] = metadata;
            }

            // Deprecated flag
            metadata.Deprecated = deprecated;
            // Summary
            if (!string.IsNullOrWhiteSpace(summary))
            {
                metadata.Summary = summary;
            }
            // Description
            if (!string.IsNullOrWhiteSpace(description))
            {
                metadata.Description = description;
            }
            // OperationId
            if (!string.IsNullOrWhiteSpace(operationId))
            {
                metadata.OperationId = operationId;
            }
        }
        return this;
    }

    /// <summary>
    /// Adds OpenAPI external documentation to this route for the given HTTP verbs.
    /// If no verbs are specified, the external documentation is applied to all verbs defined on the builder.
    /// </summary>
    /// <param name="url">The URL for the external documentation.</param>
    /// <param name="description">Optional description of the external documentation.</param>
    /// <param name="verbs">
    /// Optional HTTP verbs to which the external docs will apply.
    /// If <c>null</c> or empty, all <see cref="HttpVerb"/> are used.
    /// </param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddOpenApiExternalDoc(
        Uri url,
        string? description = null,
        IEnumerable<HttpVerb>? verbs = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);
        // If no verbs passed, apply to the builder's own verbs
        var effectiveVerbs = (verbs is null || !verbs.Any())
            ? HttpVerbs
            : verbs;

        foreach (var verb in effectiveVerbs)
        {
            if (!OpenAPI.TryGetValue(verb, out var metadata))
            {
                metadata = new OpenAPIMetadata(Pattern);
                OpenAPI[verb] = metadata;
            }

            metadata.Enabled = true;
            metadata.ExternalDocs = new OpenApiExternalDocs();

            if (!string.IsNullOrWhiteSpace(description))
            {
                metadata.ExternalDocs.Description = description;
            }

            metadata.ExternalDocs.Url = url;
        }

        return this;
    }

    /// <summary>
    /// Adds an OpenAPI parameter (by reference) to this route, either at path level or verb level.
    /// The parameter is resolved from the OpenAPI document components using a reference ID.
    /// </summary>
    /// <param name="referenceId">The reference ID of the parameter in the OpenAPI document components.</param>
    /// <param name="verbs">Optional HTTP verbs to which the parameter will be applied. If null or empty, the parameter is applied at the path level.</param>
    /// <param name="docId">Optional documentation ID. Defaults to <see cref="IOpenApiAuthenticationOptions.DefaultSchemeName"/>.</param>
    /// <param name="description">Optional description override for the parameter (applied only at verb level, like the PowerShell version).</param>
    /// <param name="embed">If true, the parameter definition is cloned and embedded into the route. If false, a reference-based parameter is used.</param>
    /// <param name="key">Optional key to set the parameter name when embedding. Ignored for reference-only parameters.</param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddOpenApiParameter(
        string referenceId,
        IEnumerable<HttpVerb>? verbs = null,
        string? docId = null,
        string? description = null,
        bool embed = false,
        string? key = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);
        // Default DocId as in PowerShell:
        // [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultSchemeName
        docId ??= IOpenApiAuthenticationOptions.DefaultSchemeName;

        // Determine if we’re using path-level metadata (Verbs.Count -eq 0)
        var usePathLevel = verbs is null || !verbs.Any();

        // Resolve parameter from OpenAPI document components
        var docDescriptor = Server.OpenApiDocumentDescriptor[docId];
        if (docDescriptor.Document?.Components == null)
        {
            throw new InvalidOperationException(
                $"The OpenAPI document with ID '{docId}' does not contain any components.");
        }
        var parameters = docDescriptor.Document.Components.Parameters ?? throw new InvalidOperationException(
                $"The OpenAPI document with ID '{docId}' does not contain any parameter components.");

        if (!parameters.TryGetValue(referenceId, out var componentParameter))
        {
            throw new InvalidOperationException(
                $"Parameter with ReferenceId '{referenceId}' does not exist in the OpenAPI document components.");
        }

        IOpenApiParameter parameter;

        if (embed)
        {
            // Clone the component
            var cloned = OpenApiComponentClone.Clone(componentParameter);

            // Set parameter name if provided
            if (!string.IsNullOrEmpty(key))
            {
                if (cloned is OpenApiParameter param)
                {
                    param.Name = key;
                }
            }

            parameter = cloned;
        }
        else
        {
            // Reference-based parameter
            var parameterRef = new OpenApiParameterReference(referenceId);
            parameter = parameterRef;
        }

        if (usePathLevel)
        {
            // Path-level metadata
            PathLevelOpenAPIMetadata ??= new OpenAPICommonMetadata();
            PathLevelOpenAPIMetadata.Parameters ??= [];
            PathLevelOpenAPIMetadata.Parameters.Add(parameter);
        }
        else
        {
            // Verb-level metadata
            foreach (var verb in verbs!)
            {
                if (!OpenAPI.TryGetValue(verb, out var metadata))
                {
                    metadata = new OpenAPIMetadata(Pattern);
                    OpenAPI[verb] = metadata;
                }
                // Ensure metadata is enabled
                metadata.Enabled = true;

                // Description override (only applied at verb level in your PS version)
                if (!string.IsNullOrEmpty(description))
                {
                    parameter.Description = description;
                }

                metadata.Parameters ??= [];
                metadata.Parameters.Add(parameter);
            }
        }

        return this;
    }


    /// <summary>
    /// Adds an OpenAPI request body (by reference) to this route for the given HTTP verbs.
    /// The request body is resolved from the OpenAPI document components using a reference ID.
    /// </summary>
    /// <param name="referenceId">
    /// The reference ID of the request body in the OpenAPI document components.
    /// </param>
    /// <param name="verbs">Optional HTTP verbs to which the request body will be applied.If null or empty, the request body is applied to all verbs defined on <see cref="HttpVerb"/>.</param>
    /// <param name="docId">Optional documentation ID. Defaults to <see cref="IOpenApiAuthenticationOptions.DefaultSchemeName"/>. </param>
    /// <param name="description">Optional description override for the request body.</param>
    /// <param name="force">If true, allows adding a request body to verbs that usually don’t support one (GET, HEAD).TRACE is always skipped.</param>
    /// <param name="embed">If true, the request body definition is cloned and embedded.If false, a reference-based request body is used.</param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddOpenApiRequestBody(
        string referenceId,
        IEnumerable<HttpVerb>? verbs = null,
        string? docId = null,
        string? description = null,
        bool force = false,
        bool embed = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(Pattern);
        // Default DocId like in PS: [IOpenApiAuthenticationOptions]::DefaultSchemeName
        docId ??= IOpenApiAuthenticationOptions.DefaultSchemeName;

        // If no verbs passed, apply to the builder's own verbs
        var effectiveVerbs = (verbs is null || !verbs.Any())
            ? HttpVerbs
            : verbs;

        // Resolve request bodies from OpenAPI components
        var docDescriptor = Server.OpenApiDocumentDescriptor[docId];
        if (docDescriptor.Document?.Components == null)
        {
            throw new InvalidOperationException(
                $"The OpenAPI document with ID '{docId}' does not contain any components.");
        }
        var requestBodies = docDescriptor.Document.Components.RequestBodies ?? throw new InvalidOperationException(
                $"The OpenAPI document with ID '{docId}' does not contain any request body components.");

        if (!requestBodies.TryGetValue(referenceId, out var componentRequestBody))
        {
            throw new InvalidOperationException(
                $"RequestBody with ReferenceId '{referenceId}' does not exist in the OpenAPI document components.");
        }

        foreach (var verb in effectiveVerbs)
        {
            // Skip verbs that typically don't support request bodies unless forced.
            var isGetOrHead = verb is HttpVerb.Get or HttpVerb.Head;
            var isTrace = verb == HttpVerb.Trace;

            if ((isGetOrHead && !force) || isTrace)
            {
                // In PS you log or Write-Warning; here you can hook Serilog or any logger
                Server.Logger?.Warning(
                    "Cannot add RequestBody to HTTP verb {Verb} as it does not support a request body.",
                    verb);

                continue;
            }

            if (!OpenAPI.TryGetValue(verb, out var metadata))
            {
                metadata = new OpenAPIMetadata(Pattern);
                OpenAPI[verb] = metadata;
            }

            if (metadata.RequestBody is not null)
            {
                throw new InvalidOperationException(
                    $"RequestBody already defined for verb {verb} in this MapRouteBuilder.");
            }

            // Build request body: embedded clone or reference
            var requestBody = embed ?
                OpenApiComponentClone.Clone(componentRequestBody) :
                new OpenApiRequestBodyReference(referenceId);

            // Mark as having OpenAPI metadata
            metadata.Enabled = true;

            // Description override if provided
            if (!string.IsNullOrEmpty(description))
            {
                requestBody.Description = description;
            }

            metadata.RequestBody = requestBody;
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

    /// <summary>
    /// Adds authorization requirements (schemes + policies) to this route and
    /// configures corresponding OpenAPI security metadata.
    /// </summary>
    /// <param name="policies">Authorization policy names required for the route. These are also treated as scopes for the OpenAPI security requirements. </param>
    /// <param name="verbs">Optional HTTP verbs to which the authorization will be applied.If null or empty, the authorization is applied to all verbs defined in <see cref="HttpVerb"/>.</param>
    /// <param name="schema">Optional explicit authentication scheme name to include in the security requirements.</param>
    /// <returns>The same <see cref="MapRouteBuilder"/> instance for chaining.</returns>
    public MapRouteBuilder AddAuthorization(
        IEnumerable<string>? policies = null,
        IEnumerable<HttpVerb>? verbs = null,
        string? schema = null)
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
        var allSchemes = new List<string>();

        foreach (var verb in effectiveVerbs)
        {
            if (!OpenAPI.TryGetValue(verb, out var metadata))
            {
                metadata = new OpenAPIMetadata(Pattern);
                OpenAPI[verb] = metadata;
            }

            metadata.Enabled = true;

            // Ensure Security list exists:
            // List<Dictionary<string, IEnumerable<string>>>
            metadata.Security ??= new List<Dictionary<string, List<string>>>();
            // d: Dictionary<string, List<string>> (we'll convert values to IEnumerable<string> later)
            var tempScopesByScheme = new Dictionary<string, List<string>>();

            // Start with the explicit schema, if any
            if (!string.IsNullOrWhiteSpace(schema))
            {
                tempScopesByScheme[schema] = new List<string>();
                if (!allSchemes.Contains(schema))
                {
                    allSchemes.Add(schema);
                }
            }

            // For each policy, resolve schemes and map scheme -> scopes (policies)
            foreach (var policy in policyList)
            {
                var schemesForPolicy = Server.RegisteredAuthentications.GetSchemesByPolicy(policy);
                if (schemesForPolicy is null) { continue; }

                foreach (var sc in schemesForPolicy)
                {

                    if (!tempScopesByScheme.TryGetValue(sc, out var scopeList))
                    {
                        scopeList = [];
                        tempScopesByScheme[sc] = scopeList;
                    }
                    if (scopeList != null && scopeList is List<string> list && !list.Contains(policy))
                    {
                        list.Add(policy);
                    }

                    if (!allSchemes.Contains(sc))
                    {
                        allSchemes.Add(sc);
                    }
                }
            }

            // Convert List<string> -> IEnumerable<string> for the OpenAPI model
            var securityRequirement = tempScopesByScheme.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            );

            metadata.Security.Add(securityRequirement);
        }

        // Add schemes to RequireSchemes if any
        if (allSchemes.Count > 0)
        {
            // Assuming RequireSchemes is a List<string> or similar collection
            if (RequireSchemes is List<string> list)
            {
                list.AddRange(allSchemes);
            }
            else if (RequireSchemes is ICollection<string> coll)
            {
                foreach (var s in allSchemes)
                {
                    coll.Add(s);
                }
            }
        }

        // Add policies if any
        if (policyList.Count > 0)
        {
            if (RequirePolicies is List<string> list)
            {
                list.AddRange(policyList);
            }
            else if (RequirePolicies is ICollection<string> coll)
            {
                foreach (var p in policyList)
                {
                    coll.Add(p);
                }
            }
        }

        return this;
    }
}

<#
.SYNOPSIS
    Registers the built-in aggregated health endpoint for the active Kestrun server.
.DESCRIPTION
    Wraps the Kestrun host health extension to add (or replace) the HTTP endpoint that aggregates
    all registered health probes. Most parameters are optional and override the defaults provided by
    the active host configuration. When no overrides are supplied the endpoint is registered at
    '/health' and inherits the server's existing security posture.
.PARAMETER Server
    The Kestrun host instance to configure. If omitted, the current server context is resolved automatically.
.PARAMETER Pattern
    The relative URL pattern for the endpoint (for example '/healthz'). Must begin with '/'.
.PARAMETER DefaultTags
    Optional collection of probe tags to evaluate when the incoming request does not specify an explicit tag filter.
.PARAMETER AllowAnonymous
    Set to $true to permit anonymous calls or $false to require authentication. When omitted, the existing configuration is used.
.PARAMETER TreatDegradedAsUnhealthy
    Set to $true to return HTTP 503 when any probe reports a degraded state. Defaults to $false.
.PARAMETER ThrowOnDuplicate
    When $true, throws if another GET route already exists at the specified pattern. Otherwise the endpoint is skipped with a warning.
.PARAMETER RequireSchemes
    Optional list of authentication schemes required to access the endpoint.
.PARAMETER RequirePolicies
    Optional list of authorization policies required to access the endpoint.
.PARAMETER CorsPolicyName
    Optional ASP.NET Core CORS policy name applied to the endpoint.
.PARAMETER RateLimitPolicyName
    Optional ASP.NET Core rate limiting policy name applied to the endpoint.
.PARAMETER ShortCircuit
    Set to $true to short-circuit the pipeline with the health result.
.PARAMETER ShortCircuitStatusCode
    Overrides the status code used when ShortCircuit is $true.
.PARAMETER OpenApiSummary
    Overrides the OpenAPI summary documented for the endpoint.
.PARAMETER OpenApiDescription
    Overrides the OpenAPI description documented for the endpoint.
.PARAMETER OpenApiOperationId
    Overrides the OpenAPI operation id documented for the endpoint.
.PARAMETER OpenApiTags
    Overrides the OpenAPI tag list documented for the endpoint.
.PARAMETER OpenApiGroupName
    Overrides the OpenAPI group name documented for the endpoint.
.PARAMETER MaxDegreeOfParallelism
    Limits the number of concurrent probes executed during a health request.
.PARAMETER ProbeTimeout
    Adjusts the timeout enforced for each probe during a health request.
.PARAMETER DefaultScriptLanguage
    Overrides the default script language used when registering script-based probes.
.PARAMETER ResponseContentType
    Controls the response payload format returned by the endpoint (Json, Yaml, Xml, or Auto for content negotiation).
.PARAMETER XmlRootElementName
    When emitting XML output, overrides the root element name (defaults to 'Response'). Ignored for non-XML formats.
.PARAMETER Compress
    When set, JSON and XML output are compact (no indentation). By default output is human readable.
.PARAMETER PassThru
    Emits the configured server instance so the call can be chained.
.EXAMPLE
    Add-KrHealthEndpoint -Pattern '/healthz' -TreatDegradedAsUnhealthy $true -ProbeTimeout '00:00:05'
    Registers a health endpoint at /healthz that fails when probes report a degraded status and enforces a 5 second probe timeout.
.EXAMPLE
    Get-KrServer | Add-KrHealthEndpoint -OpenApiTags 'Diagnostics','Monitoring' -PassThru
    Adds the health endpoint to the current server, customises OpenAPI metadata, and returns the server for further configuration.
#>
function Add-KrHealthEndpoint {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [ValidatePattern('^/')]
        [string]$Pattern,

        [string[]]$DefaultTags,

        [bool]$AllowAnonymous,

        [bool]$TreatDegradedAsUnhealthy,

        [bool]$ThrowOnDuplicate,

        [string[]]$RequireSchemes,

        [string[]]$RequirePolicies,

        [string]$CorsPolicyName,

        [string]$RateLimitPolicyName,

        [bool]$ShortCircuit,

        [int]$ShortCircuitStatusCode,

        [string]$OpenApiSummary,

        [string]$OpenApiDescription,

        [string]$OpenApiOperationId,

        [string[]]$OpenApiTags,

        [string]$OpenApiGroupName,

        [ValidateRange(1, [int]::MaxValue)]
        [int]$MaxDegreeOfParallelism,

        [timespan]$ProbeTimeout,

        [Kestrun.Scripting.ScriptLanguage]$DefaultScriptLanguage,

        [Kestrun.Health.HealthEndpointContentType]$ResponseContentType,

        [string]$XmlRootElementName,

        [switch]$Compress,

        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        $options = $Server.Options.Health
        if ($null -ne $options) {
            $options = $options.Clone()
        } else {
            $options = [Kestrun.Health.HealthEndpointOptions]::new()
        }

        if ($PSBoundParameters.ContainsKey('Pattern')) {
            $options.Pattern = $Pattern
        }

        if ($PSBoundParameters.ContainsKey('DefaultTags')) {
            $options.DefaultTags = @($DefaultTags | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        }

        if ($PSBoundParameters.ContainsKey('AllowAnonymous')) {
            $options.AllowAnonymous = $AllowAnonymous
        }

        if ($PSBoundParameters.ContainsKey('TreatDegradedAsUnhealthy')) {
            $options.TreatDegradedAsUnhealthy = $TreatDegradedAsUnhealthy
        }

        if ($PSBoundParameters.ContainsKey('ThrowOnDuplicate')) {
            $options.ThrowOnDuplicate = $ThrowOnDuplicate
        }

        if ($PSBoundParameters.ContainsKey('RequireSchemes')) {
            $options.RequireSchemes = @($RequireSchemes)
        }

        if ($PSBoundParameters.ContainsKey('RequirePolicies')) {
            $options.RequirePolicies = @($RequirePolicies)
        }

        if ($PSBoundParameters.ContainsKey('CorsPolicyName')) {
            $options.CorsPolicyName = $CorsPolicyName
        }

        if ($PSBoundParameters.ContainsKey('RateLimitPolicyName')) {
            $options.RateLimitPolicyName = $RateLimitPolicyName
        }

        if ($PSBoundParameters.ContainsKey('ShortCircuit')) {
            $options.ShortCircuit = $ShortCircuit
        }

        if ($PSBoundParameters.ContainsKey('ShortCircuitStatusCode')) {
            $options.ShortCircuitStatusCode = $ShortCircuitStatusCode
        }

        if ($PSBoundParameters.ContainsKey('OpenApiSummary')) {
            $options.OpenApiSummary = $OpenApiSummary
        }

        if ($PSBoundParameters.ContainsKey('OpenApiDescription')) {
            $options.OpenApiDescription = $OpenApiDescription
        }

        if ($PSBoundParameters.ContainsKey('OpenApiOperationId')) {
            $options.OpenApiOperationId = $OpenApiOperationId
        }

        if ($PSBoundParameters.ContainsKey('OpenApiTags')) {
            $options.OpenApiTags = @($OpenApiTags)
        }

        if ($PSBoundParameters.ContainsKey('OpenApiGroupName')) {
            $options.OpenApiGroupName = $OpenApiGroupName
        }

        if ($PSBoundParameters.ContainsKey('MaxDegreeOfParallelism')) {
            $options.MaxDegreeOfParallelism = $MaxDegreeOfParallelism
        }

        if ($PSBoundParameters.ContainsKey('ProbeTimeout')) {
            if ($ProbeTimeout -le [timespan]::Zero) {
                throw 'ProbeTimeout must be greater than zero.'
            }
            $options.ProbeTimeout = $ProbeTimeout
        }

        if ($PSBoundParameters.ContainsKey('DefaultScriptLanguage')) {
            $options.DefaultScriptLanguage = $DefaultScriptLanguage
        }

        if ($PSBoundParameters.ContainsKey('ResponseContentType')) {
            $options.ResponseContentType = $ResponseContentType
        }

        if ($PSBoundParameters.ContainsKey('XmlRootElementName')) {
            $options.XmlRootElementName = $XmlRootElementName
        }

        if ($PSBoundParameters.ContainsKey('Compress')) {
            $options.Compress = $Compress.IsPresent
        }

        $result = [Kestrun.Hosting.KestrunHostHealthExtensions]::AddHealthEndpoint($Server, $options)

        if ($PassThru.IsPresent) {
            return $result
        }
    }
}

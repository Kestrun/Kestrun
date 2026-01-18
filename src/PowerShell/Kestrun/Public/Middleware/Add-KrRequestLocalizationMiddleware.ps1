<#
    .SYNOPSIS
        Adds request localization middleware to a Kestrun server instance.
    .DESCRIPTION
        The Add-KrRequestLocalizationMiddleware cmdlet configures request localization
        for a Kestrun server instance. Request localization enables applications to serve
        content in multiple languages and cultures based on the user's preferences.
        The middleware determines the culture for each request using various providers
        (query string, cookie, Accept-Language header, etc.).
    .PARAMETER Server
        The Kestrun server instance to which the request localization middleware will be added.
        If not specified, the cmdlet will attempt to use the current server context.
    .PARAMETER Options
        A Microsoft.AspNetCore.Localization.RequestLocalizationOptions object that defines
        the configuration options for the request localization middleware. If this parameter
        is provided, it takes precedence over the individual configuration parameters.
    .PARAMETER DefaultCulture
        The default culture to use when no culture can be determined from the request.
        Default is "en-US".
    .PARAMETER SupportedCultures
        An array of culture codes (e.g., "en-US", "fr-FR", "es-ES") that the application supports.
        If not specified, defaults to English (US).
    .PARAMETER SupportedUICultures
        An array of UI culture codes that the application supports for localized resources.
        If not specified, uses the same values as SupportedCultures.
    .PARAMETER FallBackToParentCultures
        A switch indicating whether to fall back to parent cultures when a specific culture is not found.
        For example, "en-GB" falls back to "en" if "en-GB" is not supported.
    .PARAMETER FallBackToParentUICultures
        A switch indicating whether to fall back to parent UI cultures.
    .PARAMETER ApplyCurrentCultureToResponseHeaders
        A switch indicating whether to apply the current culture to response headers.
    .PARAMETER PassThru
        If this switch is specified, the cmdlet will return the modified Kestrun server instance
        after adding the request localization middleware.
    .EXAMPLE
        Add-KrRequestLocalizationMiddleware -DefaultCulture "en-US" -SupportedCultures @("en-US", "fr-FR", "es-ES")
        Adds request localization middleware supporting English, French, and Spanish.
    .EXAMPLE
        Add-KrRequestLocalizationMiddleware -DefaultCulture "en-US" -SupportedCultures @("en-US", "de-DE") -FallBackToParentCultures -PassThru
        Adds request localization with fallback to parent cultures and returns the server instance.
    .EXAMPLE
        $options = [Microsoft.AspNetCore.Localization.RequestLocalizationOptions]::new()
        $options.DefaultRequestCulture = [Microsoft.AspNetCore.Localization.RequestCulture]::new("en-US")
        $options.SupportedCultures = @([System.Globalization.CultureInfo]::new("en-US"), [System.Globalization.CultureInfo]::new("fr-FR"))
        $options.SupportedUICultures = $options.SupportedCultures
        Add-KrRequestLocalizationMiddleware -Options $options -PassThru
        Creates custom RequestLocalizationOptions and adds the middleware.
    .NOTES
        This cmdlet is part of the Kestrun PowerShell module.
        The middleware uses the following providers by default (in order):
        1. QueryStringRequestCultureProvider - culture from query string (?culture=en-US&ui-culture=en-US)
        2. CookieRequestCultureProvider - culture from cookie
        3. AcceptLanguageHeaderRequestCultureProvider - culture from Accept-Language HTTP header
 #>
function Add-KrRequestLocalizationMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Localization.RequestLocalizationOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string] $DefaultCulture = 'en-US',

        [Parameter(ParameterSetName = 'Items')]
        [string[]] $SupportedCultures = @('en-US'),

        [Parameter(ParameterSetName = 'Items')]
        [string[]] $SupportedUICultures,

        [Parameter(ParameterSetName = 'Items')]
        [switch] $FallBackToParentCultures,

        [Parameter(ParameterSetName = 'Items')]
        [switch] $FallBackToParentUICultures,

        [Parameter(ParameterSetName = 'Items')]
        [switch] $ApplyCurrentCultureToResponseHeaders,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            # Create options from individual parameters
            $Options = [Microsoft.AspNetCore.Localization.RequestLocalizationOptions]::new()
            
            # Set default request culture
            $Options.DefaultRequestCulture = [Microsoft.AspNetCore.Localization.RequestCulture]::new($DefaultCulture)

            # Set supported cultures
            $Options.SupportedCultures = [System.Collections.Generic.List[System.Globalization.CultureInfo]]::new()
            foreach ($culture in $SupportedCultures) {
                $Options.SupportedCultures.Add([System.Globalization.CultureInfo]::new($culture))
            }

            # Set supported UI cultures
            if ($PSBoundParameters.ContainsKey('SupportedUICultures')) {
                $Options.SupportedUICultures = [System.Collections.Generic.List[System.Globalization.CultureInfo]]::new()
                foreach ($culture in $SupportedUICultures) {
                    $Options.SupportedUICultures.Add([System.Globalization.CultureInfo]::new($culture))
                }
            } else {
                # Create a copy of SupportedCultures
                $Options.SupportedUICultures = [System.Collections.Generic.List[System.Globalization.CultureInfo]]::new()
                foreach ($culture in $Options.SupportedCultures) {
                    $Options.SupportedUICultures.Add($culture)
                }
            }

            # Set fallback options
            if ($FallBackToParentCultures.IsPresent) {
                $Options.FallBackToParentCultures = $true
            }
            if ($FallBackToParentUICultures.IsPresent) {
                $Options.FallBackToParentUICultures = $true
            }
            if ($ApplyCurrentCultureToResponseHeaders.IsPresent) {
                $Options.ApplyCurrentCultureToResponseHeaders = $true
            }
        }

        # Add the request localization middleware
        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddRequestLocalization($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}


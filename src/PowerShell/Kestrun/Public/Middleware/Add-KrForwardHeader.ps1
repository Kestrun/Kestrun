<#
    .SYNOPSIS
        Adds Forwarded Headers middleware to a Kestrun server instance.
    .DESCRIPTION
        This cmdlet adds the Forwarded Headers middleware to a Kestrun server instance, allowing you to configure forwarded headers options.
        It can be used to process X-Forwarded-For, X-Forwarded-Proto, and other headers commonly used in reverse proxy scenarios.
    .PARAMETER Server
        The Kestrun server instance to which the Forwarded Headers middleware will be added. If not specified, the cmdlet will attempt to use the current Kestrun server instance.
    .PARAMETER Options
        A Microsoft.AspNetCore.Builder.ForwardedHeadersOptions object that defines the configuration options for the Forwarded Headers middleware.
        If this parameter is provided, it takes precedence over the individual configuration parameters (ForwardedHeaders, ForwardLimit, KnownNetworks, KnownProxies, AllowedHosts, RequireHeaderSymmetry).
    .PARAMETER ForwardedHeaders
        A Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders enum value that specifies which headers to process.
        This can be a combination of XForwardedFor, XForwardedProto, XForwardedHost, and XForwardedPathBase.
        If not specified, the default is XForwardedFor and XForwardedProto.
    .PARAMETER ForwardLimit
        An integer value that specifies the maximum number of entries to read from the X-Forwarded-For header.
        If not specified, the default is 1. A value of 0 indicates no limit.
    .PARAMETER KnownNetworks
        An array of System.Net.IPNetwork objects that represent the known networks from which forwarded headers are accepted.
        If not specified, no known networks are configured. This is typically used to restrict which proxies are trusted.
    .PARAMETER KnownProxies
        An array of System.Net.IPNetwork objects that represent the known proxies from which forwarded headers are accepted.
        If not specified, no known proxies are configured. This is typically used to restrict which proxies are trusted.
    .PARAMETER AllowedHosts
        An array of strings that represent the allowed hosts for forwarded headers.
        If not specified, no restrictions on allowed hosts are applied.
    .PARAMETER RequireHeaderSymmetry
        A switch that indicates whether header symmetry is required.
        If specified, the middleware will require that the number of X-Forwarded-For and X-Forwarded-Proto headers match.
    .PARAMETER PassThru
        If specified, the cmdlet returns the modified Kestrun server instance. This allows for further chaining of cmdlets or inspection of the server instance.
    .EXAMPLE
        Add-ForwardedHeader -ForwardedHeaders 'XForwardedFor, XForwardedProto' -KnownProxies $proxy1, $proxy2 -PassThru
        This example adds Forwarded Headers middleware to the current Kestrun server instance, configuring it to process X-Forwarded-For and X-Forwarded-Proto headers,
        trusting the specified known proxies, and returns the modified server instance.
    .EXAMPLE
        $options = [Microsoft.AspNetCore.Builder.ForwardedHeadersOptions]::new()
        $options.ForwardedHeaders = [Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders]::XForwardedFor -bor [Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders]::XForwardedProto
        $options.KnownNetworks.Add([System.Net.IPNetwork]::Parse("192.168.0.0/16"))
        Add-ForwardedHeader -Options $options -PassThru
        This example creates a ForwardedHeadersOptions object, configures it to process X-Forwarded-For and X-Forwarded-Proto headers,
        adds a known network, and then adds the Forwarded Headers middleware to the current Kestrun server instance using the specified options.
    .NOTES
        This cmdlet is part of the Kestrun PowerShell module.
        #>
function Add-ForwardedHeader {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.ForwardedHeadersOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders] $ForwardedHeaders,
        [Parameter(ParameterSetName = 'Items')]
        [int] $ForwardLimit,
        [Parameter(ParameterSetName = 'Items')]
        [System.Net.IPAddress[]] $KnownNetworks,
        [Parameter(ParameterSetName = 'Items')]
        [System.Net.IPAddress[]] $KnownProxies,
        [Parameter(ParameterSetName = 'Items')]
        [string[]] $AllowedHosts,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $RequireHeaderSymmetry,
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
            $Options = [Microsoft.AspNetCore.Builder.ForwardedHeadersOptions]::new()

            if ($PSBoundParameters.ContainsKey('ForwardedHeaders')) {
                $Options.ForwardedHeaders = $ForwardedHeaders
            }
            # Set the ForwardLimit if specified
            if ($PSBoundParameters.ContainsKey('ForwardLimit')) {
                $Options.ForwardLimit = $ForwardLimit
            }
            # Set the KnownProxies if specified
            if ($PSBoundParameters.ContainsKey('KnownProxies')) {
                $Options.KnownProxies.Clear()
                foreach ($p in $KnownProxies) {
                    $Options.KnownProxies.Add($p);
                }
            }
            # Set the KnownNetworks if specified
            if ($PSBoundParameters.ContainsKey('KnownNetworks')) {
                $Options.KnownNetworks.Clear()
                foreach ($n in $KnownNetworks) {
                    $Options.KnownNetworks.Add($n);
                }
            }
            # Set the AllowedHosts if specified
            if ($PSBoundParameters.ContainsKey('AllowedHosts')) {
                $Options.AllowedHosts.Clear()
                foreach ($h in $AllowedHosts) {
                    $Options.AllowedHosts.Add($h);
                }
            }

            #  Set the RequireHeaderSymmetry if specified
            $Options.RequireHeaderSymmetry = $RequireHeaderSymmetry.IsPresent
        }

        # Add the Forwarded Headers middleware
        [Kestrun.Hosting.KestrunSecurityMiddlewareExtensions]::AddForwardedHeaders($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}


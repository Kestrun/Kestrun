<#
.SYNOPSIS
    Adds Forwarded Headers middleware to a Kestrun server.
.DESCRIPTION
    This cmdlet adds and configures the ASP.NET Core Forwarded Headers middleware
    for a Kestrun server. This middleware processes proxy-related headers such as
    X-Forwarded-For, X-Forwarded-Proto, X-Forwarded-Host, and X-Forwarded-Prefix to
    update the request's Scheme, Host, and Remote IP address accordingly.
    This is essential when hosting behind reverse proxies or load balancers that
    modify these headers.
.PARAMETER Server
    The Kestrun server instance to which the Forwarded Headers middleware will be added.
    If not specified, the cmdlet will attempt to resolve the current server context.
.PARAMETER Options
    An instance of Microsoft.AspNetCore.Builder.ForwardedHeadersOptions to configure
    the middleware. This allows for full customization of the middleware behavior.
.PARAMETER XForwardedFor
    Switch to enable processing of the X-Forwarded-For header.
.PARAMETER XForwardedProto
    Switch to enable processing of the X-Forwarded-Proto header.
.PARAMETER XForwardedHost
    Switch to enable processing of the X-Forwarded-Host header.
.PARAMETER XForwardedPrefix
    Switch to enable processing of the X-Forwarded-Prefix header.
.PARAMETER All
    Switch to enable processing of all supported forwarded headers.
.PARAMETER ForwardLimit
    Specifies the maximum number of entries to read from the forwarded headers.
    Default is 1.
.PARAMETER KnownNetworks
    An array of IPNetwork objects representing known networks from which forwarded
    headers will be accepted.
.PARAMETER KnownProxies
    An array of IPAddress objects representing known proxy servers from which
    forwarded headers will be accepted.
.PARAMETER ForwardedForHeaderName
    Custom header name for X-Forwarded-For.
.PARAMETER ForwardedProtoHeaderName
    Custom header name for X-Forwarded-Proto.
.PARAMETER ForwardedHostHeaderName
    Custom header name for X-Forwarded-Host.
.PARAMETER ForwardedPrefixHeaderName
    Custom header name for X-Forwarded-Prefix.
.PARAMETER OriginalForHeaderName
    Custom header name for Original-For.
.PARAMETER OriginalProtoHeaderName
    Custom header name for Original-Proto.
.PARAMETER OriginalHostHeaderName
    Custom header name for Original-Host.
.PARAMETER OriginalPrefixHeaderName
    Custom header name for Original-Prefix.
.PARAMETER RequireHeaderSymmetry
    Switch to require that all enabled forwarded headers are present in the request.
.PARAMETER PassThru
    If specified, the cmdlet returns the Kestrun server instance after adding
    the middleware.
.EXAMPLE
    Add-ForwardedHeader -XForwardedFor -XForwardedProto -KnownProxies $proxyIps
    Adds Forwarded Headers middleware to the current Kestrun server, enabling
    processing of X-Forwarded-For and X-Forwarded-Proto headers, and
    trusting the specified proxy IP addresses.
.NOTES
    This cmdlet is part of the Kestrun PowerShell module.
#>
function Add-ForwardedHeader {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        # --- ParameterSet: Options (verbatim pass-through) ---
        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Builder.ForwardedHeadersOptions]$Options,

        # --- ParameterSet: Items (switches compose the enum) ---
        [Parameter(ParameterSetName = 'Items')]
        [switch]$XForwardedFor,
        [Parameter(ParameterSetName = 'Items')]
        [switch]$XForwardedProto,
        [Parameter(ParameterSetName = 'Items')]
        [switch]$XForwardedHost,
        [Parameter(ParameterSetName = 'Items')]
        [switch]$XForwardedPrefix,
        [Parameter(ParameterSetName = 'Items')]
        [switch]$All,  # convenience

        [Parameter(ParameterSetName = 'Items')]
        [int]$ForwardLimit = 1,

        [Parameter(ParameterSetName = 'Items')]
        [string[]]$KnownNetworks,
        [Parameter(ParameterSetName = 'Items')]
        [string[]]$KnownProxies,

        # Optional header name overrides (default to ForwardedHeadersDefaults)
        [Parameter(ParameterSetName = 'Items')]
        [string]$ForwardedForHeaderName,
        [Parameter(ParameterSetName = 'Items')]
        [string]$ForwardedProtoHeaderName,
        [Parameter(ParameterSetName = 'Items')]
        [string]$ForwardedHostHeaderName,
        [Parameter(ParameterSetName = 'Items')]
        [string]$ForwardedPrefixHeaderName,
        [Parameter(ParameterSetName = 'Items')]
        [string]$OriginalForHeaderName,
        [Parameter(ParameterSetName = 'Items')]
        [string]$OriginalProtoHeaderName,
        [Parameter(ParameterSetName = 'Items')]
        [string]$OriginalHostHeaderName,
        [Parameter(ParameterSetName = 'Items')]
        [string]$OriginalPrefixHeaderName,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$RequireHeaderSymmetry,

        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            # Compose ForwardedHeadersOptions from switches
            $fhEnum = [Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders]

            # Create options
            $Options = [Microsoft.AspNetCore.Builder.ForwardedHeadersOptions]::new()
            # Compose flags from switches
            if ($All) {
                $Options.ForwardedHeaders = [Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders]::All
            } else {
                $flags = $fhEnum::None
                if ($XForwardedFor) { $flags = $flags -bor $fhEnum::XForwardedFor }
                if ($XForwardedProto) { $flags = $flags -bor $fhEnum::XForwardedProto }
                if ($XForwardedHost) { $flags = $flags -bor $fhEnum::XForwardedHost }
                if ($XForwardedPrefix) { $flags = $flags -bor $fhEnum::XForwardedPrefix }
                if ($flags -eq $fhEnum::None) {
                    # Sensible default if user didn’t specify: For + Proto
                    $flags = $fhEnum::XForwardedFor -bor $fhEnum::XForwardedProto
                }
                $Options.ForwardedHeaders = $flags
            }

            # Forward limit
            $Options.ForwardLimit = $ForwardLimit

            # Known proxies
            if ($PSBoundParameters.ContainsKey('KnownProxies')) {
                $Options.KnownProxies.Clear()
                foreach ($p in $KnownProxies) {
                    $ip = if ($p -is [System.Net.IPAddress]) { $p } else { [System.Net.IPAddress]::Parse($p) }
                    [void]$Options.KnownProxies.Add($ip)
                }
            }
            # Known networks
            if ($PSBoundParameters.ContainsKey('KnownNetworks')) {
                $Options.KnownNetworks.Clear()
                foreach ($n in $KnownNetworks) {
                    # Allow "10.0.0.0/24" or IPNetwork objects directly
                    $net = if ($n -is [System.Net.IPNetwork]) { $n } else { [System.Net.IPNetwork]::Parse($n) }
                    [void]$Options.KnownNetworks.Add($net)
                }
                foreach ($net in $KnownNetworks) { [void]$Options.KnownNetworks.Add($net) }
            }
            # Custom header names
            if ($PSBoundParameters.ContainsKey('ForwardedForHeaderName')) {
                $Options.ForwardedForHeaderName = $ForwardedForHeaderName
            }
            if ($PSBoundParameters.ContainsKey('ForwardedProtoHeaderName')) {
                $Options.ForwardedProtoHeaderName = $ForwardedProtoHeaderName
            }
            if ($PSBoundParameters.ContainsKey('ForwardedHostHeaderName')) {
                $Options.ForwardedHostHeaderName = $ForwardedHostHeaderName
            }
            if ($PSBoundParameters.ContainsKey('ForwardedPrefixHeaderName')) {
                $Options.ForwardedPrefixHeaderName = $ForwardedPrefixHeaderName
            }
            if ($PSBoundParameters.ContainsKey('OriginalForHeaderName')) {
                $Options.OriginalForHeaderName = $OriginalForHeaderName
            }
            if ($PSBoundParameters.ContainsKey('OriginalProtoHeaderName')) {
                $Options.OriginalProtoHeaderName = $OriginalProtoHeaderName
            }
            if ($PSBoundParameters.ContainsKey('OriginalHostHeaderName')) {
                $Options.OriginalHostHeaderName = $OriginalHostHeaderName
            }
            if ($PSBoundParameters.ContainsKey('OriginalPrefixHeaderName')) {
                $Options.OriginalPrefixHeaderName = $OriginalPrefixHeaderName
            }

            # Symmetry
            $Options.RequireHeaderSymmetry = $RequireHeaderSymmetry.IsPresent
        }

        # Attach to server so host.Apply() can call app.UseForwardedHeaders($Options)
        $Server.ForwardedHeaderOptions = $Options

        if ($PassThru) { return $Server }
    }
}



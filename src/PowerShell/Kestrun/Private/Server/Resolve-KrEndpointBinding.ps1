<#
.SYNOPSIS
    Resolves the effective Kestrun endpoint binding from explicit parameters,
    environment variables, or built-in defaults.
.DESCRIPTION
    Binding precedence:

    1. Explicit -Uri
    2. Explicit -HostName
    3. Explicit -Port / -IPAddress
    4. ASPNETCORE_URLS environment variable
    5. PORT environment variable
    6. Built-in defaults

    Environment binding is only considered when no explicit listener target
    was supplied by the caller.
.PARAMETER PSBoundParameters
    The caller's $BoundParameters dictionary.
.PARAMETER Port
    The current Port value from the caller.
.PARAMETER IPAddress
    The current IPAddress value from the caller.
.PARAMETER HostName
    The current HostName value from the caller.
.PARAMETER Uri
    The current Uri value from the caller.
.PARAMETER DefaultPort
    The default port to use when no explicit or environment binding exists.
.PARAMETER DefaultIPAddress
    The default IP address to use when no explicit or environment binding exists.
.PARAMETER IgnoreEnvironment
    If specified, environment lookup is disabled. Enabled by default.
.OUTPUTS
    PSCustomObject with:
        - Mode   : Uri | HostName | PortIPAddress
        - Source : Explicit | Environment:ASPNETCORE_URLS | Environment:PORT | Default
        - Uri
        - HostName
        - Port
        - IPAddress
        - EndpointNames
        - RawUrl
.EXAMPLE
    $binding = Resolve-KrEndpointBinding `
        -PSBoundParameters $BoundParameters `
        -Port $Port `
        -IPAddress $IPAddress `
        -HostName $HostName `
        -Uri $Uri
#>
function Resolve-KrEndpointBinding {

    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [System.Collections.IDictionary]$BoundParameters,

        [int]$Port = 0,

        [System.Net.IPAddress]$IPAddress = [System.Net.IPAddress]::Any,

        [string]$HostName,

        [uri]$Uri,

        [int]$DefaultPort = 5000,

        [System.Net.IPAddress]$DefaultIPAddress = [System.Net.IPAddress]::Loopback,

        [switch]$IgnoreEnvironment
    )
    function Format-KrEndpointName {
        param(
            [Parameter(Mandatory)]
            [string]$Host,
            [Parameter(Mandatory)]
            [int]$Port
        )

        $parsedIp = $null
        $formattedHost = if ([System.Net.IPAddress]::TryParse($Host, [ref]$parsedIp) -and $parsedIp.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetworkV6) {
            "[$Host]"
        } else {
            $Host
        }

        return "${formattedHost}:$Port"
    }

    function Get-KrEndpointNames {
        param(
            [Parameter(Mandatory)]
            [ValidateSet('Uri', 'HostName', 'PortIPAddress')]
            [string]$Mode,
            [uri]$Uri,
            [string]$HostName,
            [int]$Port,
            [System.Net.IPAddress]$IPAddress
        )

        $names = [System.Collections.Generic.List[string]]::new()
        function Add-EndpointName {
            param([string]$Name)
            if (-not [string]::IsNullOrWhiteSpace($Name) -and -not $names.Contains($Name)) {
                $names.Add($Name)
            }
        }

        switch ($Mode) {
            'Uri' {
                if ($null -ne $Uri) {
                    Add-EndpointName (Format-KrEndpointName -Host $Uri.Host -Port $Uri.Port)
                    if ($Uri.Host -eq 'localhost') {
                        Add-EndpointName (Format-KrEndpointName -Host ([System.Net.IPAddress]::Loopback.ToString()) -Port $Uri.Port)
                        Add-EndpointName (Format-KrEndpointName -Host ([System.Net.IPAddress]::IPv6Loopback.ToString()) -Port $Uri.Port)
                    }
                }
            }

            'HostName' {
                Add-EndpointName (Format-KrEndpointName -Host $HostName -Port $Port)
                if ($HostName -eq 'localhost') {
                    Add-EndpointName (Format-KrEndpointName -Host ([System.Net.IPAddress]::Loopback.ToString()) -Port $Port)
                    Add-EndpointName (Format-KrEndpointName -Host ([System.Net.IPAddress]::IPv6Loopback.ToString()) -Port $Port)
                }
            }

            'PortIPAddress' {
                if ($null -eq $IPAddress) {
                    break
                }

                if ($IPAddress.Equals([System.Net.IPAddress]::Any) -or $IPAddress.Equals([System.Net.IPAddress]::IPv6Any)) {
                    Add-EndpointName (Format-KrEndpointName -Host 'localhost' -Port $Port)
                    Add-EndpointName (Format-KrEndpointName -Host ([System.Net.IPAddress]::Loopback.ToString()) -Port $Port)
                    Add-EndpointName (Format-KrEndpointName -Host ([System.Net.IPAddress]::IPv6Loopback.ToString()) -Port $Port)
                } else {
                    Add-EndpointName (Format-KrEndpointName -Host $IPAddress.ToString() -Port $Port)
                    if ($IPAddress.Equals([System.Net.IPAddress]::Loopback) -or $IPAddress.Equals([System.Net.IPAddress]::IPv6Loopback)) {
                        Add-EndpointName (Format-KrEndpointName -Host 'localhost' -Port $Port)
                    }
                }
            }
        }

        return [string[]]$names
    }

    function New-KrBindingResult {
        <#
        .SYNOPSIS
            Helper function to create a standardized binding result object.
        .DESCRIPTION
            This function constructs a PSCustomObject representing the resolved binding information,
            including the mode of resolution, source of the binding information, and the relevant properties.
        .PARAMETER Mode
            The mode of binding resolution: 'Uri', 'HostName', or 'PortIPAddress'.
        .PARAMETER Source
            The source of the binding information, e.g., 'Explicit', 'Environment:ASPNCORE_URLS', 'Environment:PORT', or 'Default'.
        .PARAMETER Scheme
            The URI scheme (e.g., 'http' or 'https') if applicable.
        .PARAMETER Uri
            The resolved URI if Mode is 'Uri'.
        .PARAMETER HostName
            The resolved HostName if Mode is 'HostName'.
        .PARAMETER Port
            The resolved Port if Mode is 'PortIPAddress'.
        .PARAMETER IPAddress
            The resolved IPAddress if Mode is 'PortIPAddress'.
        .PARAMETER RawUrl
            The original URL string used for logging and diagnostics, especially when the binding was derived from environment variables.
        .OUTPUTS
            A PSCustomObject containing the binding resolution details.
        #>
        [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
        param(
            [Parameter(Mandatory)][ValidateSet('Uri', 'HostName', 'PortIPAddress')] [string]$Mode,
            [Parameter(Mandatory)][string]$Source,
            [string]$Scheme,
            [uri]$Uri,
            [string]$HostName,
            [int]$Port,
            [System.Net.IPAddress]$IPAddress,
            [string]$RawUrl
        )

        [pscustomobject]@{
            Mode = $Mode
            Source = $Source
            Scheme = $Scheme
            Uri = $Uri
            HostName = $HostName
            Port = $Port
            IPAddress = $IPAddress
            EndpointNames = @(Get-KrEndpointNames -Mode $Mode -Uri $Uri -HostName $HostName -Port $Port -IPAddress $IPAddress)
            RawUrl = $RawUrl
        }
    }

    function Convert-KrUrlToBinding {
        <#
        .SYNOPSIS
            Converts a URL string into a structured binding result object.
        .DESCRIPTION
            This function takes a URL string, validates it, and extracts the relevant components to create a standardized binding result.
            It handles special cases for wildcard hosts and ensures that the URL is well-formed and contains the necessary information for binding.
        .PARAMETER Url
            The URL string to convert into a binding result.
        .PARAMETER Source
            A string indicating the source of the URL, used for logging and diagnostics.
        .OUTPUTS
            A PSCustomObject containing the binding resolution details derived from the URL.
        #>
        param(
            [Parameter(Mandatory)][string]$Url,
            [string]$Source = 'Environment'
        )

        $trimmed = $Url.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            throw 'Binding URL is empty.'
        }

        # Handle ASP.NET-style wildcard hosts that [uri] doesn't like directly.
        if ($trimmed -match '^(?<scheme>https?)://(?<host>\+|\*|\[[^\]]+\]|[^:/]+)(:(?<port>\d+))?/?$') {
            $scheme = $Matches['scheme']
            $hostname = $Matches['host']
            $portText = $Matches['port']

            if (-not $portText) {
                throw "Binding URL '$trimmed' does not specify a port."
            }

            $resolvedPort = [int]$portText

            switch ($hostname) {
                '+' {
                    return New-KrBindingResult -Mode 'PortIPAddress' -Source $Source -Port $resolvedPort -IPAddress ([System.Net.IPAddress]::Any) -Scheme $scheme -RawUrl $trimmed
                }
                '*' {
                    return New-KrBindingResult -Mode 'PortIPAddress' -Source $Source -Port $resolvedPort -IPAddress ([System.Net.IPAddress]::Any) -Scheme $scheme -RawUrl $trimmed
                }
                '0.0.0.0' {
                    return New-KrBindingResult -Mode 'PortIPAddress' -Source $Source -Port $resolvedPort -IPAddress ([System.Net.IPAddress]::Any) -Scheme $scheme -RawUrl $trimmed
                }
                '::' {
                    return New-KrBindingResult -Mode 'PortIPAddress' -Source $Source -Port $resolvedPort -IPAddress ([System.Net.IPAddress]::IPv6Any) -Scheme $scheme -RawUrl $trimmed
                }
                'localhost' {
                    return New-KrBindingResult -Mode 'HostName' -Source $Source -HostName 'localhost' -Port $resolvedPort -Scheme $scheme -RawUrl $trimmed
                }
                default {
                    $parsedIp = $null
                    if ([System.Net.IPAddress]::TryParse($hostname.Trim('[', ']'), [ref]$parsedIp)) {
                        return New-KrBindingResult -Mode 'PortIPAddress' -Source $Source -Port $resolvedPort -IPAddress $parsedIp -Scheme $scheme -RawUrl $trimmed
                    }

                    return New-KrBindingResult -Mode 'HostName' -Source $Source -HostName $hostname -Port $resolvedPort -Scheme $scheme -RawUrl $trimmed
                }
            }
        }

        try {
            $parsedUri = [uri]$trimmed
        } catch {
            throw "Invalid binding URL '$trimmed'. $($_.Exception.Message)"
        }

        if (-not $parsedUri.IsAbsoluteUri) {
            throw "Binding URL '$trimmed' must be an absolute URI."
        }

        if ($parsedUri.Port -lt 0) {
            throw "Binding URL '$trimmed' must specify a port."
        }

        return New-KrBindingResult -Mode 'Uri' -Source $Source -Scheme $parsedUri.Scheme -Uri $parsedUri -RawUrl $trimmed
    }

    $hasUri = $BoundParameters.ContainsKey('Uri')
    $hasHostName = $BoundParameters.ContainsKey('HostName')
    $hasPort = $BoundParameters.ContainsKey('Port')
    $hasIPAddress = $BoundParameters.ContainsKey('IPAddress')

    # 1. Explicit URI
    if ($hasUri) {
        return New-KrBindingResult -Mode 'Uri' -Source 'Explicit' -Uri $Uri -RawUrl $Uri.AbsoluteUri
    }

    # 2. Explicit HostName
    if ($hasHostName) {
        $effectivePort = if ($hasPort) { $Port } elseif ($Port -gt 0) { $Port } else { $DefaultPort }
        return New-KrBindingResult -Mode 'HostName' -Source 'Explicit' -HostName $HostName -Port $effectivePort
    }

    # 3. Explicit Port/IPAddress
    if ($hasPort -or $hasIPAddress) {
        $effectivePort = if ($hasPort) { $Port } elseif ($Port -gt 0) { $Port } else { $DefaultPort }
        $effectiveIP = if ($hasIPAddress) { $IPAddress } elseif ($null -ne $IPAddress) { $IPAddress } else { $DefaultIPAddress }

        return New-KrBindingResult -Mode 'PortIPAddress' -Source 'Explicit' -Port $effectivePort -IPAddress $effectiveIP
    }

    # 4. Environment: ASPNETCORE_URLS
    if (-not $IgnoreEnvironment) {
        $aspnetcoreUrls = [Environment]::GetEnvironmentVariable('ASPNETCORE_URLS')
        if (-not [string]::IsNullOrWhiteSpace($aspnetcoreUrls)) {
            $firstUrl = ($aspnetcoreUrls -split '\s*;\s*' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)
            if ($firstUrl) {
                return Convert-KrUrlToBinding -Url $firstUrl -Source 'Environment:ASPNETCORE_URLS'
            }
        }

        # 5. Environment: PORT
        $portValue = [Environment]::GetEnvironmentVariable('PORT')
        if (-not [string]::IsNullOrWhiteSpace($portValue)) {
            $parsedPort = 0
            if (-not [int]::TryParse($portValue, [ref]$parsedPort) -or $parsedPort -le 0 -or $parsedPort -gt 65535) {
                throw "Environment variable PORT has invalid value '$portValue'. Expected an integer between 1 and 65535."
            }

            return New-KrBindingResult -Mode 'PortIPAddress' -Source 'Environment:PORT' -Port $parsedPort -IPAddress ([System.Net.IPAddress]::Any)
        }
    }

    # 6. Defaults
    $fallbackPort = if ($Port -gt 0) { $Port } else { $DefaultPort }
    $fallbackIp = if ($null -ne $IPAddress) { $IPAddress } else { $DefaultIPAddress }

    return New-KrBindingResult -Mode 'PortIPAddress' -Source 'Default' -Port $fallbackPort -IPAddress $fallbackIp
}

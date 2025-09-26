<#
    .SYNOPSIS
        Registers an HTTP-based health probe that polls a remote endpoint.
    .DESCRIPTION
        Creates a Kestrun HttpProbe that issues a GET request to the specified URL and interprets the
        response according to the standard health contract. Provide a shared HttpClient instance for
        production use to avoid socket exhaustion, or rely on the default constructed client for simple
        scenarios.
    .PARAMETER Server
        The Kestrun host instance to configure. If omitted, the current server context is resolved automatically.
    .PARAMETER Name
        Unique name for the probe.
    .PARAMETER Url
        The absolute URL that the probe polls.
    .PARAMETER Tags
        Optional set of tags used to include or exclude the probe when requests filter by tag.
    .PARAMETER HttpClient
        Optional HttpClient reused for the probe requests. When omitted a new HttpClient instance is created.
    .PARAMETER Timeout
        Optional timeout applied to the HTTP request. Defaults to 5 seconds.
    .PARAMETER PassThru
        Emits the configured server instance so the call can be chained.
    .EXAMPLE
        Add-KrHealthHttpProbe -Name Api -Url 'https://api.contoso.local/health' -Tags 'remote','api'
        Registers a health probe that checks a downstream API health endpoint.
    .EXAMPLE
        $client = [System.Net.Http.HttpClient]::new()
        Get-KrServer | Add-KrHealthHttpProbe -Name Ping -Url 'https://example.com/health' -HttpClient $client -PassThru
        Registers a probe using a shared HttpClient instance and returns the host for additional configuration.
#>
function Add-KrHealthHttpProbe {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$Url,

        [string[]]$Tags,

        [System.Net.Http.HttpClient]$HttpClient,

        [timespan]$Timeout,

        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Call New-KrServer first or pipe an existing host instance.'
        }
    }
    process {
        if (-not [Uri]::IsWellFormedUriString($Url, [UriKind]::Absolute)) {
            throw "The URL '$Url' must be an absolute URI."
        }

        $normalizedTags = @()
        if ($PSBoundParameters.ContainsKey('Tags')) {
            $normalizedTags = @($Tags | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
        }

        if ($normalizedTags.Count -eq 0) {
            $normalizedTags = @()
        }

        $client = $HttpClient
        if (-not $PSBoundParameters.ContainsKey('HttpClient') -or $null -eq $HttpClient) {
            $client = [System.Net.Http.HttpClient]::new()
        }

        $probe = if ($PSBoundParameters.ContainsKey('Timeout')) {
            if ($Timeout -le [timespan]::Zero) {
                throw 'Timeout must be greater than zero.'
            }
            [Kestrun.Health.HttpProbe]::new($Name, $normalizedTags, $client, $Url, $Timeout)
        } else {
            [Kestrun.Health.HttpProbe]::new($Name, $normalizedTags, $client, $Url)
        }

        try {
            $hostResult = $Server.AddProbe($probe)
            Write-KrLog -Level Information -Message "HTTP health probe '{0}' registered." -Properties $Name
            if ($PassThru.IsPresent) {
                return $hostResult
            }
        } catch {
            Write-KrLog -Level Error -Message "Failed to register HTTP health probe '{0}'." -Properties $Name -ErrorRecord $_
            throw
        }
    }
}

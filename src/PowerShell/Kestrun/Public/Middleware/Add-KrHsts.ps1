<#
    .SYNOPSIS
        Adds HTTP Strict Transport Security (HSTS) middleware to a Kestrun server instance.
    .DESCRIPTION
        The Add-KrHsts cmdlet configures HTTP Strict Transport Security (HSTS)
        for a Kestrun server instance. HSTS is a web security policy mechanism that helps
        to protect websites against protocol downgrade attacks and cookie hijacking.
        It allows web servers to declare that web browsers (or other complying user agents)
        should only interact with it using secure HTTPS connections, and never via the insecure HTTP protocol.
    .PARAMETER Server
        The Kestrun server instance to which the HSTS middleware will be added.
        If not specified, the cmdlet will attempt to use the current server context.
    .PARAMETER Options
        A Microsoft.AspNetCore.HttpsPolicy.HstsOptions object that defines the configuration options for
        the HSTS middleware. If this parameter is provided, it takes precedence over the individual configuration
        parameters (MaxAgeDays, IncludeSubDomains, Preload, ExcludedHosts).
    .PARAMETER MaxAgeDays
        The maximum duration (in days) that the browser should remember that a site is only to be accessed using HTTPS.
        The default value is 30 days.
    .PARAMETER IncludeSubDomains
        A switch indicating whether the HSTS policy should also apply to all subdomains of the site.
        If this switch is present, the IncludeSubDomains directive will be included in the HSTS header.
    .PARAMETER Preload
        A switch indicating whether the site should be included in browsers' HSTS preload list.
        If this switch is present, the Preload directive will be included in the HSTS header.
    .PARAMETER ExcludedHosts
        An array of hostnames that should be excluded from the HSTS policy. These hosts will not receive the HSTS header.
    .PARAMETER AllowInDevelopment
        A switch that allows HSTS to work in development environments by clearing the default excluded hosts.
        By default, ASP.NET Core excludes localhost and development hosts from HSTS for security.
        Use this switch to enable HSTS for testing and development scenarios.
    .PARAMETER PassThru
        If this switch is specified, the cmdlet will return the modified Kestrun server instance
        after adding the HSTS middleware. This allows for further chaining of cmdlets or inspection of
        the server instance.
    .EXAMPLE
        Add-KrHsts -MaxAgeDays 60 -IncludeSubDomains -Preload -PassThru
        This example adds HSTS middleware to the current Kestrun server instance with a max age of 60 days,
        includes subdomains, enables preload, and returns the modified server instance.
    .EXAMPLE
        Add-KrHsts -MaxAgeDays 30 -IncludeSubDomains -Preload -AllowInDevelopment
        This example enables HSTS for development/testing by clearing default excluded hosts.
        Useful for testing HSTS behavior in non-production environments.
    .EXAMPLE
        $options = [Microsoft.AspNetCore.HttpsPolicy.HstsOptions]::new()
        $options.MaxAge = [TimeSpan]::FromDays(90)
        $options.IncludeSubDomains = $true
        Add-KrHsts -Options $options -PassThru
        This example creates a HstsOptions object with a max age of 90 days and includes subdomains,
        then adds the HSTS middleware to the current Kestrun server instance and returns the modified server instance.
    .NOTES
        This cmdlet is part of the Kestrun PowerShell module.
 #>
function Add-KrHsts {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.HttpsPolicy.HstsOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [ValidateRange(1, [int]::MaxValue)]
        [int] $MaxAgeDays = 30,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $IncludeSubDomains,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $Preload,
        [Parameter(ParameterSetName = 'Items')]
        [string[]] $ExcludedHosts,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $AllowInDevelopment,
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
            $Options = [Microsoft.AspNetCore.HttpsPolicy.HstsOptions]::new()
            # Set default values
            $Options.MaxAge = [TimeSpan]::FromDays($MaxAgeDays)

            if ($PSBoundParameters.ContainsKey('IncludeSubDomains')) { $Options.IncludeSubDomains = $IncludeSubDomains.IsPresent }
            if ($PSBoundParameters.ContainsKey('Preload')) { $Options.Preload = $Preload.IsPresent }

            # Handle AllowInDevelopment switch - clears default excluded hosts first
            if ($AllowInDevelopment.IsPresent) {
                $Options.ExcludedHosts.Clear()
            }

            # Add any explicitly specified excluded hosts
            if ($PSBoundParameters.ContainsKey('ExcludedHosts')) {
                foreach ($h in $ExcludedHosts) {
                    $Options.ExcludedHosts.Add($h);
                }
            }
        }

        # Add the HTTPS redirection middleware
        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddHsts($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}


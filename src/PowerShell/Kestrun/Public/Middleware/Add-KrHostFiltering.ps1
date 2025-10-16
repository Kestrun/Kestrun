<#
    .SYNOPSIS
        Adds Host Filtering middleware to a Kestrun server instance.
    .DESCRIPTION
        This cmdlet adds the Host Filtering middleware to a Kestrun server instance, allowing you to configure host filtering options.
    .PARAMETER Server
        The Kestrun server instance to which the Host Filtering middleware will be added. If not specified, the cmdlet will attempt to use the current Kestrun server instance.
    .PARAMETER Options
        A Microsoft.AspNetCore.HostFiltering.HostFilteringOptions object that defines the configuration options for the Host Filtering middleware.
        If this parameter is provided, it takes precedence over the individual configuration parameters (AllowedHosts, AllowEmptyHosts, IncludeFailureMessage).
    .PARAMETER AllowedHosts
        The hosts headers that are allowed to access this site. At least one value is required.
        Port numbers must be excluded.
        A top level wildcard "*" allows all non-empty hosts.
        Subdomain wildcards are permitted. E.g. "*.example.com" matches subdomains like foo.example.com, but not the parent domain example.com.
        Unicode host names are allowed but will be converted to punycode for matching.
        IPv6 addresses must include their bounding brackets and be in their normalized form.
    .PARAMETER NotAllowEmptyHosts
        A switch indicating whether requests with an empty Host header should be allowed.
        If this switch is present, requests with an empty Host header will be rejected.
    .PARAMETER ExcludeFailureMessage
        A switch indicating whether to exclude the failure message in the response when a request is rejected due to host filtering.
        If this switch is present, the failure message will be excluded from the response.
    .PARAMETER PassThru
        If this switch is specified, the cmdlet will return the modified Kestrun server instance
        after adding the Host Filtering middleware. This allows for further chaining of cmdlets or inspection of
        the server instance.
    .EXAMPLE
        Add-KrHostFiltering -AllowedHosts "example.com", "www.example.com" -PassThru
        This example adds Host Filtering middleware to the current Kestrun server instance, allowing only requests with Host headers
        matching "example.com" or "www.example.com", and returns the modified server instance.
    .EXAMPLE
        $options = [Microsoft.AspNetCore.HostFiltering.HostFilteringOptions]::new()
        $options.AllowedHosts.Add("example.com")
        $options.AllowEmptyHosts = $true
        Add-KrHostFiltering -Options $options -PassThru
        This example creates a HostFilteringOptions object that allows requests with the Host header "example.com"
        and allows empty Host headers, then adds the Host Filtering middleware to the current Kestrun server instance and returns the modified server instance.
    .NOTES
        This cmdlet is part of the Kestrun PowerShell module.

 #>
function Add-KrHostFiltering {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.HostFiltering.HostFilteringOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string[]] $AllowedHosts,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $NotAllowEmptyHosts,
        [Parameter(ParameterSetName = 'Items')]
        [switch] $ExcludeFailureMessage,
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
            $Options = [Microsoft.AspNetCore.HostFiltering.HostFilteringOptions]::new()

            if ($PSBoundParameters.ContainsKey('AllowedHosts')) {
                # Validate that at least one host is provided
                if ($AllowedHosts.Count -eq 0) {
                    throw 'At least one AllowedHost must be specified when using the AllowedHosts parameter.'
                }
                foreach ($h in $AllowedHosts) {
                    $Options.AllowedHosts.Add($h);
                }
            }
            # Validate the options
            if ($PSBoundParameters.ContainsKey('NotAllowEmptyHosts')) { $Options.AllowEmptyHosts = -not $NotAllowEmptyHosts.IsPresent }
            if ($PSBoundParameters.ContainsKey('ExcludeFailureMessage')) { $Options.IncludeFailureMessage = -not $ExcludeFailureMessage.IsPresent }
        }

        # Add the Host Filtering middleware
        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddHostFiltering($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}


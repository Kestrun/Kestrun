<#
.SYNOPSIS
    Adds a CORS policy to the Kestrun server.
.DESCRIPTION
    This function adds a CORS policy to the Kestrun server. It can be used to configure CORS policies for the Kestrun server.
.PARAMETER Builder
    The CORS policy builder to be used to create the CORS policy.
.PARAMETER Server
    The Kestrun server to which the CORS policy will be added.
.PARAMETER Name
    The name of the CORS policy to be added.
.PARAMETER Default
    Specifies that the CORS policy is the default policy.
.PARAMETER AllowAll
    Specifies that the CORS policy should allow all origins, methods, and headers.
.PARAMETER PassThru
    Specifies that the CORS policy builder should be passed through the pipeline.
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsMethod -Any | Set-KrCorsHeader -Any | Add-KrCorsPolicy -Server $server -Name 'MyCORSPolicy'
.EXAMPLE
    New-KrCorsPolicyBuilder | Set-KrCorsMethod -Any | Set-KrCorsHeader -Any | Add-KrCorsPolicy -Server $server -Default
.EXAMPLE
    Add-KrCorsPolicy -Server $server -Default -AllowAll
.EXAMPLE
    Add-KrCorsPolicy -Server $server -Default -Name 'MyCORSPolicy'
.NOTES
    This function is part of the Kestrun runtime API and is used to configure CORS policies for the Kestrun server.
#>
function Add-KrCorsPolicy {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', '')]
    [CmdletBinding(DefaultParameterSetName = 'Named')]
    [OutputType([Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder])]
    param(
        # Builder path (pipeline-friendly)
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, ParameterSetName = 'Named')]
        [Parameter(Mandatory = $true, ValueFromPipeline = $true, ParameterSetName = 'Default')]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        # Server
        [Parameter()]
        [Kestrun.Hosting.KestrunHost]$Server,

        # Named policy
        [Parameter(Mandatory = $true, ParameterSetName = 'AllowAll-Named')]
        [Parameter(Mandatory = $true, ParameterSetName = 'Named')]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        # Default policy
        [Parameter(Mandatory = $true, ParameterSetName = 'Default')]
        [Parameter(Mandatory = $true, ParameterSetName = 'AllowAll-Default')]
        [switch]$Default,

        # Convenience: allow everything (no builder required)
        [Parameter(Mandatory = $true, ParameterSetName = 'AllowAll-Named')]
        [Parameter(Mandatory = $true, ParameterSetName = 'AllowAll-Default')]
        [switch]$AllowAll,

        # Pipeline continuation
        [Parameter()]
        [Alias('PassBuilder')]
        [switch]$PassThru
    )

    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }

    process {
        if ($AllowAll.IsPresent -and $PSBoundParameters.ContainsKey('Builder')) {
            throw 'Do not pipe a builder when using -AllowAll. Use -AllowAll without a builder.'
        }

        if ($AllowAll.IsPresent) {
            # Allow all origins, methods, and headers
            if ([string]::IsNullOrEmpty($Name)) {
                [Kestrun.Hosting.KestrunSecurityMiddlewareExtensions]::AddCorsDefaultPolicyAllowAll($Server) | Out-Null
            } else {
                [Kestrun.Hosting.KestrunSecurityMiddlewareExtensions]::AddCorsPolicyAllowAll($Server, $Name) | Out-Null
            }
        } else {
            # Use the provided builder to add the CORS policy
            if ([string]::IsNullOrEmpty($Name)) {
                [Kestrun.Hosting.KestrunSecurityMiddlewareExtensions]::AddCorsDefaultPolicy($Server, $Builder) | Out-Null
            } else {
                [Kestrun.Hosting.KestrunSecurityMiddlewareExtensions]::AddCorsPolicy($Server, $Name, $Builder) | Out-Null
            }
        }
        # Pass the builder through the pipeline if requested
        if ($PassThru.IsPresent) {
            return $Builder
        }
    }
}

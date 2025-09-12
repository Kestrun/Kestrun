<#
.SYNOPSIS
    Adds an authorization policy to a Kestrun host.
.DESCRIPTION
    This cmdlet adds an authorization policy to a Kestrun host instance.
    The policy can require specific claims and/or roles, and can specify authentication schemes.
.PARAMETER Server
    The Kestrun host instance to which the policy will be added. If not specified, the default server is used.
.PARAMETER Name
    The name of the authorization policy.
.PARAMETER RequireClaims
    An array of claims that are required for the policy.
.PARAMETER RequireRoles
    An array of roles that are required for the policy.
.PARAMETER AuthenticationSchemes
    An array of authentication schemes to use for the policy. Default is 'Negotiate'.
.PARAMETER PassThru
    If specified, the cmdlet returns the modified Kestrun host instance.
.EXAMPLE
    Add-KrAuthorizationPolicy -Name "AdminPolicy" -RequireRoles "Admin" -RequireClaims (New-Object System.Security.Claims.Claim("Department", "IT"))
    Adds an authorization policy named "AdminPolicy" that requires the "Admin" role and a claim of type "Department" with value "IT".
#>
function Add-KrAuthorizationPolicy {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter()]
        [System.Security.Claims.Claim[]]$RequireClaims,
        [Parameter()]
        [string[]]$RequireRoles,
        [Parameter()]
        [string[]]$AuthenticationSchemes = @('Negotiate'),
        [Parameter()]
        [switch]$PassThru
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        $newConfig = [Kestrun.Authorization.KestrunPolicyConfig]::new()
        $newConfig.Name = $Name
        if ($null -ne $RequireClaims) { $newConfig.RequiredClaims = $RequireClaims }
        if ($null -ne $RequireRoles) { $newConfig.RequiredRoles = $RequireRoles }
        if ($null -ne $AuthenticationSchemes) { $newConfig.AuthenticationSchemes = $AuthenticationSchemes }

        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddAuthorization($Server, $newConfig ) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

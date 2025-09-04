<#
    .SYNOPSIS
        Builds the claim policy configuration from the builder.
    .DESCRIPTION
        This function finalizes the claim policy construction by invoking the Build method on the ClaimPolicyBuilder instance.
    .PARAMETER Builder
        The claim policy builder to finalize.
    .OUTPUTS
        [Kestrun.Claims.ClaimPolicyConfig]
        The constructed claim policy configuration.
    .EXAMPLE
        $policyConfig = New-KrClaimPolicy | Add-KrClaimPolicy -PolicyName "AdminOnly" -ClaimType "role" -AllowedValues "admin" | Build-KrClaimPolicy
        This example creates a new claim policy builder, adds a policy, and then builds the claim policy configuration.
    .NOTES
        This function is part of the Kestrun.Claims module and is used to build claim policies
        Maps to ClaimPolicyBuilder.Build
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/kestrun.authentication.claimpolicybuilder.build
#>
function Build-KrClaimPolicy {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Kestrun.Claims.ClaimPolicyConfig])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Claims.ClaimPolicyBuilder] $Builder
    )

    process {
        return $Builder.Build()
    }
}


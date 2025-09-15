<#
    .SYNOPSIS
        Creates a new claim policy builder instance.
    .DESCRIPTION
        This function initializes a new instance of the Kestrun.Claims.ClaimPolicyBuilder class, which is used to build claim policies.
    .OUTPUTS
        [Kestrun.Claims.ClaimPolicyBuilder]
        A new instance of the claim policy builder.
    .EXAMPLE
        $builder = New-KrClaimPolicy
        This example creates a new claim policy builder instance.
    .NOTES
        This function is part of the Kestrun.Claims module and is used to manage claim policies.
        Maps to ClaimPolicyBuilder constructor.
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/kestrun.authentication.claimpolicybuilder
#>
function New-KrClaimPolicy {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [OutputType([Kestrun.Claims.ClaimPolicyBuilder])]
    param( )
    return [Kestrun.Claims.ClaimPolicyBuilder]::new()
}


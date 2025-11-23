<#
    .SYNOPSIS
        Adds a new claim policy to the KestrunClaims system.
    .DESCRIPTION
        This function allows you to define a new claim policy by specifying the policy name, claim type, and allowed values.
    .PARAMETER Builder
        The claim policy builder instance used to create the policy.
    .PARAMETER PolicyName
        The name of the policy to be created.
    .PARAMETER ClaimType
        The type of claim being defined.
    .PARAMETER Description
        The description of the claim policy.
    .PARAMETER UserClaimType
        The user identity claim type.
    .PARAMETER AllowedValues
        The values that are allowed for this claim.
    .EXAMPLE
        PS C:\> Add-KrClaimPolicy -Builder $builder -PolicyName "ExamplePolicy" -ClaimType "ExampleClaim" -AllowedValues "Value1", "Value2"
        This is an example of how to use the Add-KrClaimPolicy function.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build Claims
#>
function Add-KrClaimPolicy {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'ClaimType')]
    [OutputType([Kestrun.Claims.ClaimPolicyBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Claims.ClaimPolicyBuilder] $Builder,
        [Parameter(Mandatory = $true)]
        [string] $PolicyName,
        [Parameter(Mandatory = $true, ParameterSetName = 'ClaimType')]
        [string] $ClaimType,
        [Parameter(Mandatory = $true, ParameterSetName = 'UserClaimType')]
        [Kestrun.Claims.UserIdentityClaim] $UserClaimType,
        [Parameter(Mandatory = $true)]
        [string[]] $AllowedValues,
        [Parameter(Mandatory = $false)]
        [string]$Description
    )
    begin {
        if ($UserClaimType) {
            $ClaimType = [Kestrun.Claims.KestrunClaimExtensions]::ToClaimUri($UserClaimType)
        }
    }
    process {
        return $Builder.AddPolicy($PolicyName, $ClaimType, $Description, $AllowedValues)
    }
}


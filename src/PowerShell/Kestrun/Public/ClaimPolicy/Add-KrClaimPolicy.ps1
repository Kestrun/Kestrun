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
.PARAMETER Scope
    If specified, indicates that the claim type is a scope.
.PARAMETER Description
    The description of the claim policy.
.PARAMETER UserClaimType
    The user identity claim type.
.PARAMETER AllowedValues
    The values that are allowed for this claim.
.EXAMPLE
    PS C:\> Add-KrClaimPolicy -Builder $builder -PolicyName "ExamplePolicy" -ClaimType "ExampleClaim" -AllowedValues "Value1", "Value2"
    This is an example of how to use the Add-KrClaimPolicy function.
    It creates a claim policy named "ExamplePolicy" for the claim type "ExampleClaim" with allowed values "Value1" and "Value2".
.EXAMPLE
    PS C:\> Add-KrClaimPolicy -Builder $builder -PolicyName "ScopePolicy" -Scope -AllowedValues "read", "write"
    This example creates a claim policy named "ScopePolicy" for the claim type "scope" with allowed values "read" and "write".
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

        [Parameter(Mandatory = $true, ParameterSetName = 'Scope')]
        [switch]$Scope,

        [Parameter(Mandatory = $true, ParameterSetName = 'ClaimType')]
        [Parameter(Mandatory = $true, ParameterSetName = 'UserClaimType')]
        [string[]] $AllowedValues,

        [Parameter(Mandatory = $false)]
        [string]$Description
    )
    begin {
        # Determine claim type based on parameter set
        if ($Scope) {
            $ClaimType = 'scope'
            $AllowedValues = @($PolicyName)
        } elseif ($UserClaimType) {
            # Use user identity claim type
            $ClaimType = [Kestrun.Claims.KestrunClaimExtensions]::ToClaimUri($UserClaimType)
        }
    }
    process {
        return $Builder.AddPolicy($PolicyName, $ClaimType, $Description, $AllowedValues)
    }
}


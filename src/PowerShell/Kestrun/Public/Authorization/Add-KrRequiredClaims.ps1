<#
.SYNOPSIS
    Adds required claims to a collection of claims for authorization purposes.
.DESCRIPTION
    This cmdlet takes an existing array of claims and adds new required claims based on the specified claim type and allowed values.
    It can be used to build a collection of claims that can be passed to an authorization policy.
.PARAMETER Claims
    An existing array of claims to which the new required claims will be added. This parameter is optional.
.PARAMETER ClaimType
    The type of claim to add. This can be specified using the UserIdentityClaim enum.
.PARAMETER AllowedValues
    An array of allowed values for the specified claim type. This parameter is mandatory.
.OUTPUTS
    An array of System.Security.Claims.Claim objects, including the newly added required claims.
.EXAMPLE
    $claims = @()
    $requiredClaims = Add-KrRequiredClaims -Claims $claims -ClaimType UserIdentityClaim -AllowedValues "user1", "user2"
    This example creates an empty array of claims and adds a required claim of type UserIdentityClaim with allowed values "user1" and "user2".
    The resulting $requiredClaims array will contain the new claim.
.NOTES
    This cmdlet is designed to be used in the context of Kestrun authorization policies.
#>
function Add-KrRequiredClaim {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([System.Security.Claims.Claim[]])]
    [OutputType([System.Array])]
    param(
        [Parameter(ValueFromPipeline)]
        [System.Security.Claims.Claim[]] $Claims,
        [Kestrun.Claims.UserIdentityClaim] $ClaimType,
        [Parameter(Mandatory = $true)]
        [string[]] $AllowedValues
    )

    begin { $bag = [System.Collections.Generic.List[System.Security.Claims.Claim]]::new() }

    process { if ($null -ne $Claims) { $bag.AddRange($Claims) } }

    end {
        # resolve ClaimType if the user chose the enum parameter-set
        if ($UserClaimType) {
            $ClaimType = [Kestrun.Claims.KestrunClaimExtensions]::ToClaimUri($UserClaimType)
        }

        $bag.Add([System.Security.Claims.Claim]::new($ClaimType, $AllowedValues))

        # OUTPUT: one strongly-typed array, not enumerated
        , ([System.Security.Claims.Claim[]] $bag.ToArray())
    }
}

<#
    .SYNOPSIS
        Adds a claim to the current user.
        This function allows you to add a new claim to the current user's identity.
        The claim can be of any type, and you must specify the claim type and value.
        The function will return the updated claims collection.
    .DESCRIPTION
        This function is designed to be used in the context of Kestrun for managing user claims.
        It supports both string-based claim types and user identity claims.
        Claims can be added to the user's identity using this function.
        This function is part of the Kestrun.Claims module and is used to manage user claims.
        It maps to ClaimCollection.Add method.
    .PARAMETER Claims
        The claims to add to the current user's identity. This can be a single claim or an array of claims.
        If this parameter is specified, the ClaimType and Value parameters are ignored.
        If this parameter is not specified, you must provide the ClaimType and Value parameters.
    .PARAMETER ClaimType
        The type of claim to add to the user's identity. This is required if the Claims parameter is not specified.
        It can be a string representing the claim type or a Kestrun.Claims.UserIdentityClaim enum value.
    .PARAMETER UserClaimType
        The user identity claim type to use when adding the claim. This is required if the Claims parameter is not specified.
        It must be a valid Kestrun.Claims.UserIdentityClaim enum value.
    .PARAMETER Value
        The value of the claim to add to the user's identity. This is required if the Claims parameter is not specified.
        It can be a string or a Kestrun.Claims.UserIdentityClaim enum value.
        If the Claims parameter is specified, this parameter is ignored.

    .EXAMPLE
        Adds a claim to the current user's identity.
        This example demonstrates how to add a claim using the ClaimType and Value parameters.
        Add-KrUserClaim -ClaimType "customClaimType" -Values "customClaimValue"
    .EXAMPLE
        Adds a claim to the current user's identity.
        This example demonstrates how to add a claim using the UserClaimType and Value parameters.
        Add-KrUserClaim -UserClaimType "Email" -Values "user@example.com"
    .NOTES
        This function is part of the Kestrun.Claims module and is used to manage user claims.
        It maps to ClaimCollection.Add method.
#>
function Add-KrUserClaim {
    [KestrunRuntimeApi('Route')]
    [CmdletBinding(DefaultParameterSetName = 'ClaimType')]
    [OutputType([System.Security.Claims.Claim[]])]
    [OutputType([System.Array])]
    param(
        [Parameter(ValueFromPipeline)]
        [System.Security.Claims.Claim[]] $Claims,
        [Parameter(Mandatory = $true, ParameterSetName = 'ClaimType')]
        [string] $ClaimType,
        [Parameter(Mandatory = $true, ParameterSetName = 'UserClaimType')]
        [Kestrun.Claims.UserIdentityClaim] $UserClaimType,
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    begin { $bag = [System.Collections.Generic.List[System.Security.Claims.Claim]]::new() }

    process { if ($null -ne $Claims) { $bag.AddRange($Claims) } }

    end {
        # resolve ClaimType if the user chose the enum parameter-set
        if ($UserClaimType) {
            $ClaimType = [Kestrun.Claims.KestrunClaimExtensions]::ToClaimUri($UserClaimType)
        }

        $bag.Add([System.Security.Claims.Claim]::new($ClaimType, $Value))

        # OUTPUT: one strongly-typed array, not enumerated
        , ([System.Security.Claims.Claim[]] $bag.ToArray())
    }
}


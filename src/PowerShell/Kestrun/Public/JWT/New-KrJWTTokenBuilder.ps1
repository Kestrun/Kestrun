<#
    .SYNOPSIS
        Creates a new JWT token builder instance.
    .DESCRIPTION
        This function initializes a new instance of the JwtTokenBuilder class, which is used to construct JWT tokens.
    .EXAMPLE
        # Creates a new JWT token builder instance
        $builder = New-KrJWTBuilder
    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        A new instance of the JwtTokenBuilder class.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens.
        Maps to JwtTokenBuilder.New
#>
function New-KrJWTBuilder {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param()
    # Create a new JWT token builder instance
    return [Kestrun.Jwt.JwtTokenBuilder]::New()
}


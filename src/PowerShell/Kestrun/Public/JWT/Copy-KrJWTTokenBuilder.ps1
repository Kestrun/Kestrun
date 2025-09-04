<#
    .SYNOPSIS
       Creates a new cloned JWT token builder instance.
    .DESCRIPTION
       This function creates a new cloned instance of the JwtTokenBuilder class, which is used to construct JWT tokens.
    .PARAMETER Builder
        The original JWT token builder instance to clone.
    .EXAMPLE
       # Creates a new cloned JWT token builder instance
       $builder = $oldBuilder|New-KrJWTToken

    .EXAMPLE
       # Creates a new cloned JWT token builder instance
       $builder = New-KrJWTToken -Builder $oldBuilder

       $builder.WithSubject('admin')
               .WithIssuer('https://issuer')
               .WithAudience('api')
               .SignWithSecret('uZ6zDP3CGK3rktmVOXQk8A')   # base64url
               .EncryptWithCertificate($cert,'RSA-OAEP','A256GCM')
               .Build()

    .OUTPUTS
        [Kestrun.Jwt.JwtTokenBuilder]
        A new cloned instance of the JwtTokenBuilder class.
    .NOTES
        This function is part of the Kestrun.Jwt module and is used to build JWT tokens.
        Maps to JwtTokenBuilder.New
#>
function Copy-KrJWTTokenBuilder {
    [KestrunRuntimeApi('Everywhere')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Kestrun.Jwt.JwtTokenBuilder])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline)]
        [Kestrun.Jwt.JwtTokenBuilder] $Builder
    )
    process {
        # Create a new JWT token builder instance
        return $Builder.CloneBuilder()
    }
}


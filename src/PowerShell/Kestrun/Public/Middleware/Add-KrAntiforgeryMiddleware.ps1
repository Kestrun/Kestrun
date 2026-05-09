<#
    .SYNOPSIS
        Adds an Antiforgery service to the server.
    .DESCRIPTION
        This cmdlet allows you to configure the Antiforgery service for the Kestrun server.
        It can be used to protect against Cross-Site Request Forgery (CSRF) attacks by generating and validating antiforgery tokens.
    .PARAMETER Options
        The Antiforgery options to configure the service.
    .PARAMETER CookieName
        The name of the cookie to use for the Antiforgery token. Default is '.Kestrun.AntiXSRF'.
    .PARAMETER FormFieldName
        The name of the form field to use for the Antiforgery token. If not specified, the default will be used.
    .PARAMETER HeaderName
        The name of the header to use for the Antiforgery token. Default is 'X-CSRF-TOKEN'.
    .PARAMETER SuppressXFrameOptionsHeader
        If specified, the X-Frame-Options header will not be added to responses.
    .PARAMETER SuppressReadingTokenFromFormBody
        If specified, the Antiforgery service will not read tokens from the form body. This option is only available in .NET 9.0+ / PowerShell 7.6+.
    .EXAMPLE
        Add-KrAntiforgeryMiddleware -Cookie $cookieBuilder -FormField '__RequestVerificationToken' -HeaderName 'X-CSRF-Token' -SuppressXFrameOptionsHeader
        This example adds an Antiforgery service to the server with a custom cookie builder, form field name, and header name.
    .EXAMPLE
        Add-KrAntiforgeryMiddleware -Options $options
        This example adds an Antiforgery service to the server using the specified Antiforgery options.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.antiforgery.antiforgeryoptions?view=aspnetcore-8.0
#>
function Add-KrAntiforgeryMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    param(

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string]$FormFieldName,

        [Parameter(ParameterSetName = 'Items')]
        [string]$CookieName = '.Kestrun.AntiXSRF',

        [Parameter(ParameterSetName = 'Items')]
        [string]$HeaderName = 'X-CSRF-TOKEN',

        [Parameter(ParameterSetName = 'Items')]
        [switch]$SuppressXFrameOptionsHeader,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$SuppressReadingTokenFromFormBody
    )
    # Ensure the server instance is resolved
    $Server = Resolve-KestrunServer

    if ($PSCmdlet.ParameterSetName -eq 'Items') {
        $Options = [Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions]::new()

        # build default cookie
        $cookie = [Microsoft.AspNetCore.Http.CookieBuilder]::new()
        $cookie.Name = $CookieName
        $cookie.SameSite = [Microsoft.AspNetCore.Http.SameSiteMode]::Lax
        $cookie.HttpOnly = $true
        $cookie.SecurePolicy = [Microsoft.AspNetCore.Http.CookieSecurePolicy]::Always
        $cookie.Path = '/'

        $Options.Cookie = $cookie

        if (-not [string]::IsNullOrEmpty($FormFieldName)) {
            $Options.FormFieldName = $FormFieldName
        }
        if (-not [string]::IsNullOrEmpty($HeaderName)) {
            $Options.HeaderName = $HeaderName
        }
        if ($SuppressXFrameOptionsHeader.IsPresent) {
            $Options.SuppressXFrameOptionsHeader = $true
        }
        if (Test-KrCapability -Feature 'SuppressReadingTokenFromFormBody') {
            if ($SuppressReadingTokenFromFormBody.IsPresent) {
                $Options.SuppressReadingTokenFromFormBody = $true
            }
        }
    }

    # Add the Antiforgery service to the server
    [Kestrun.Hosting.KestrunSecurityMiddlewareExtensions]::AddAntiforgery($Server, $Options) | Out-Null
}

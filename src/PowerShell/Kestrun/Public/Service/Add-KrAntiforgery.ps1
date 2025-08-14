
function Add-KrAntiforgery {
    <#
    .SYNOPSIS
        Adds an Antiforgery service to the server.
    .DESCRIPTION
        This cmdlet allows you to configure the Antiforgery service for the Kestrun server.
        It can be used to protect against Cross-Site Request Forgery (CSRF) attacks by generating and validating antiforgery tokens.
    .PARAMETER Server
        The Kestrun server instance to which the Antiforgery service will be added.
    .PARAMETER Options
        The Antiforgery options to configure the service.
    .PARAMETER Cookie
        The cookie builder to use for the Antiforgery service.
    .PARAMETER FormFieldName
        The name of the form field to use for the Antiforgery token.
    .PARAMETER HeaderName
        The name of the header to use for the Antiforgery token.
    .PARAMETER SuppressXFrameOptionsHeader
        If specified, the X-Frame-Options header will not be added to responses.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after adding the Antiforgery service.
    .EXAMPLE
        $server | Add-KrAntiforgery -Cookie $cookieBuilder -FormField '__RequestVerificationToken' -HeaderName 'X-CSRF-Token' -SuppressXFrameOptionsHeader
        This example adds an Antiforgery service to the server with a custom cookie builder, form field name, and header name.
    .EXAMPLE
        $server | Add-KrAntiforgery -Options $options
        This example adds an Antiforgery service to the server using the specified Antiforgery options.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.antiforgery.antiforgeryoptions?view=aspnetcore-8.0
    #>
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [Microsoft.AspNetCore.Http.CookieBuilder]$Cookie = $null,

        [Parameter(ParameterSetName = 'Items')]
        [string]$FormFieldName,

        [Parameter(ParameterSetName = 'Items')]
        [string]$HeaderName,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$SuppressXFrameOptionsHeader,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions]::new()
            if ($null -ne $Cookie) {
                $Options.Cookie = $Cookie
            }
            if (-not [string]::IsNullOrEmpty($FormFieldName)) {
                $Options.FormFieldName = $FormFieldName
            }
            if (-not [string]::IsNullOrEmpty($HeaderName)) {
                $Options.HeaderName = $HeaderName
            }
            if ($SuppressXFrameOptionsHeader.IsPresent) {
                $Options.SuppressXFrameOptionsHeader = $true
            }
        } 
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        
        # Add the Antiforgery service to the server
        [Kestrun.Hosting.KestrunHostStaticFilesExtensions]::AddAntiforgery($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}
<#
    .SYNOPSIS
        Adds a CORS policy to the server.
    .DESCRIPTION
        This cmdlet allows you to configure a CORS policy for the Kestrun server.
        It can be used to specify allowed origins, methods, headers, and other CORS settings.
    .PARAMETER Server
        The Kestrun server instance to which the CORS policy will be added.
    .PARAMETER Name
        The name of the CORS policy.
    .PARAMETER Builder
        The CORS policy builder to configure the CORS policy.
    .PARAMETER AllowAnyOrigin
        If specified, allows any origin to access the resources.
    .PARAMETER AllowAnyMethod
        If specified, allows any HTTP method to be used in requests.
    .PARAMETER AllowAnyHeader
        If specified, allows any header to be included in requests.
        If not specified, only headers explicitly allowed will be included.
    .PARAMETER AllowCredentials
        If specified, allows credentials (cookies, authorization headers, etc.) to be included in requests.
    .PARAMETER DisallowCredentials
        If specified, disallows credentials in requests.
        If not specified, credentials will be allowed.
    .PARAMETER PassThru
        If specified, returns the modified server instance after adding the CORS policy.
    .EXAMPLE
        $server | Add-KrCorsPolicyMiddleware -Name 'AllowAll' -AllowAnyOrigin -AllowAnyMethod -AllowAnyHeader
        This example adds a CORS policy named 'AllowAll' to the server, allowing any origin, method, and header.
    .EXAMPLE
        $server | Add-KrCorsPolicyMiddleware -Name 'CustomPolicy' -Builder $builder
        This example adds a CORS policy named 'CustomPolicy' to the server using the specified CORS policy builder.
    .EXAMPLE
        $server | Add-KrCorsPolicyMiddleware -Server $server -Name 'CustomPolicy' -AllowAnyOrigin -AllowAnyMethod -AllowAnyHeader
        This example adds a CORS policy named 'CustomPolicy' to the server, allowing any origin, method, and header.
    .NOTES
        This cmdlet is used to configure CORS policies for the Kestrun server, allowing you to control cross-origin requests and specify which origins, methods, and headers are allowed.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.cors.infrastructure.corspolicybuilder?view=aspnetcore-8.0
#>
function Add-KrCorsPolicyMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]$Builder,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$AllowAnyOrigin,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$AllowAnyMethod,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$AllowAnyHeader,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$AllowCredentials,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$DisallowCredentials,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {

            if ($AllowCredentials.IsPresent -and $DisallowCredentials.IsPresent) {
                throw 'Cannot specify both AllowCredentials and DisallowCredentials.'
            }

            $Builder = [Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder]::new()
            if ($AllowAnyOrigin.IsPresent) {
                $Builder.AllowAnyOrigin() | Out-Null
            }
            if ($AllowAnyMethod.IsPresent) {
                $Builder.AllowAnyMethod() | Out-Null
            }
            if ($AllowAnyHeader.IsPresent) {
                $Builder.AllowAnyHeader() | Out-Null
            }
            if ($AllowCredentials.IsPresent) {
                $Builder.AllowCredentials() | Out-Null
            }
            if ($DisallowCredentials.IsPresent) {
                $Builder.DisallowCredentials() | Out-Null
            }
        }

        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddCors($Server, $Name, $Builder) | Out-Null
        # Add the CORS policy to the server

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}


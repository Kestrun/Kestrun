<#
    .SYNOPSIS
        Adds HTTPS redirection middleware to the Kestrun server.
    .DESCRIPTION
        This cmdlet allows you to add HTTPS redirection middleware to a Kestrun server instance.
        It can be used to enforce HTTPS by redirecting HTTP requests to HTTPS.
    .PARAMETER Server
        The Kestrun server instance to which the HTTPS redirection middleware will be added.
    .PARAMETER Options
        An instance of HttpsRedirectionOptions to configure the HTTPS redirection behavior.
        If this parameter is provided, it takes precedence over the individual parameters.
    .PARAMETER RedirectStatusCode
        The HTTP status code to use for redirection. Default is 307 (Temporary Redirect).
        This parameter is ignored if the Options parameter is provided.
    .PARAMETER HttpsPort
        The HTTPS port to which requests should be redirected. If not specified, the default port (443) is used.
        This parameter is ignored if the Options parameter is provided.
    .PARAMETER PassThru
        If specified, the cmdlet returns the modified Kestrun server instance.
    .EXAMPLE
        Add-KrHttpsRedirection -Server $myServer -RedirectStatusCode 301 -HttpsPort 8443
        Adds HTTPS redirection middleware to the specified Kestrun server instance,
        using a 301 (Permanent Redirect) status code and redirecting to port 8443.
    .EXAMPLE
        $options = [Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions]::new()
        $options.RedirectStatusCode = 308
        $options.HttpsPort = 8443
        Add-KrHttpsRedirection -Server $myServer -Options $options -PassThru
        Adds HTTPS redirection middleware to the specified Kestrun server instance,
        using the provided HttpsRedirectionOptions and returns the modified server instance.
    .NOTES
        This cmdlet is part of the Kestrun PowerShell module.
 #>
function Add-KrHttpsRedirection {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [int]$RedirectStatusCode = [Microsoft.AspNetCore.Http.StatusCodes]::Status307TemporaryRedirect,

        [Parameter(ParameterSetName = 'Items')]
        [int]$HttpsPort,
        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            # Create options from individual parameters
            $Options = [Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions]::new()
            # Set default values
            $Options.RedirectStatusCode = $RedirectStatusCode

            if ($PSBoundParameters.ContainsKey('HttpsPort')) { $Options.HttpsPort = $HttpsPort }
        }

        # Add the HTTPS redirection middleware
        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddHttpsRedirection($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}


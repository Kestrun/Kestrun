<#
    .SYNOPSIS
        Adds Windows authentication to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use Windows authentication for incoming requests.
        This allows the server to authenticate users based on their Windows credentials.
        This enables the server to use Kerberos or NTLM for authentication.
    .PARAMETER Server
        The Kestrun server instance to configure.
    .PARAMETER PassThru
        If specified, returns the modified server instance after adding the authentication.
    .EXAMPLE
        Add-KrWindowsAuthentication -Server $myServer -PassThru
        This example adds Windows authentication to the specified Kestrun server instance and returns the modified instance.
    .LINK
        https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.windowsauthentication?view=aspnetcore-8.0
    .NOTES
        This cmdlet is used to configure Windows authentication for the Kestrun server, allowing you to secure your APIs with Windows credentials.
#>
function Add-KrWindowsAuthentication {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'ItemsScriptBlock')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline)]
        [Kestrun.Hosting.KestrunHost]$Server,

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
        # Add Windows authentication to the server instance ---
        [Kestrun.Hosting.KestrunHostAuthnExtensions]::AddWindowsAuthentication($Server) | Out-Null
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}


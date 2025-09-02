<#
    .SYNOPSIS
        Enables Kestrun server configuration and starts the server.
    .DESCRIPTION
        This function applies the configuration to the Kestrun server and starts it.
    .PARAMETER Server
        The Kestrun server instance to configure and start. This parameter is mandatory.
    .PARAMETER Quiet
        If specified, suppresses output messages during the configuration and startup process.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance after applying the configuration.
    .EXAMPLE
        Enable-KrConfiguration -Server $server
        Applies the configuration to the specified Kestrun server instance and starts it.
    .NOTES
        This function is designed to be used after the server has been configured with routes, listeners,
        and other middleware components.
#>
function Enable-KrConfiguration {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSAvoidUsingWriteHost', '')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [switch]$Quiet,
        [Parameter()]
        [switch]$PassThru
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        $psDefaultVars = Get-KrPSDefaultVariable
        $dict = [System.Collections.Generic.Dictionary[string, System.Object]]::new()
        # Get the user-defined variables
        $userVars = Get-Variable -Scope Script
        $userVars += Get-Variable -Scope Global

        $userVars | Where-Object { $psDefaultVars -notcontains $_.Name -and
            $_.Name -notmatch '^_' } | ForEach-Object {
            $dict[$_.Name] = $_.Value
        }

        # Set the user-defined variables in the server configuration
        $Server.EnableConfiguration($dict) | Out-Null
        if (-not $Quiet.IsPresent) {
            Write-Host 'Kestrun server configuration enabled successfully.'
            Write-Host "Server Name: $($Server.Options.ApplicationName)"
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}

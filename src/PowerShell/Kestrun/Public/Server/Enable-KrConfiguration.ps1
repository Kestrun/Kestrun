<#
    .SYNOPSIS
        Enables Kestrun server configuration and starts the server.
    .DESCRIPTION
        This function applies the configuration to the Kestrun server and starts it.
    .PARAMETER Server
        The Kestrun server instance to configure and start. This parameter is mandatory.
    .PARAMETER ExcludeVariables
        An array of variable names to exclude from the runspaces.
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
        [string[]]$ExcludeVariables,
        [Parameter()]
        [switch]$Quiet,
        [Parameter()]
        [switch]$PassThru
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        $Variables = Get-KrAssignedVariable -FromParent -ResolveValues -IncludeSetVariable

        $dict = [System.Collections.Generic.Dictionary[string, System.Object]]::new()
        $Variables | ForEach-Object {
            if ($ExcludeVariables -notcontains $_.Name) {
                $dict[$_.Name] = $_.Value.Value
            }
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

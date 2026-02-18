<#
.SYNOPSIS
    Configures a custom PowerShell scriptblock to build error responses for route execution failures.
.DESCRIPTION
    Sets or clears a host-level custom PowerShell error response script. When configured, PowerShell
    route execution error paths invoke this scriptblock instead of the default WriteErrorResponseAsync
    behavior. If the custom script is not configured or fails, Kestrun falls back to the default behavior.
.PARAMETER Server
    The Kestrun server instance. If omitted, the current server is resolved.
.PARAMETER ScriptBlock
    Scriptblock invoked during PowerShell route error handling. The script runs in the request runspace
    and can use variables: $Context, $KrContext, $StatusCode, $ErrorMessage, and $Exception.
.PARAMETER Clear
    Clears the currently configured custom PowerShell error response script.
.PARAMETER WhatIf
    Shows what would happen if the cmdlet runs. The cmdlet is not run.
.PARAMETER Confirm
    Prompts for confirmation before running the cmdlet.
.EXAMPLE
    Set-KrPowerShellErrorResponse -ScriptBlock {
        Write-KrJsonResponse @{ error = $ErrorMessage; status = $StatusCode } -StatusCode $StatusCode
    }

    Configures a custom JSON error payload for PowerShell route execution errors.
.EXAMPLE
    Set-KrPowerShellErrorResponse -Clear

    Clears the custom PowerShell error response script and restores default error handling.
.NOTES
    Configure this before Enable-KrConfiguration.
#>
function Set-KrPowerShellErrorResponse {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Set', SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Set')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'Clear')]
        [switch]$Clear
    )

    process {
        $Server = Resolve-KestrunServer -Server $Server

        if ($Clear.IsPresent) {
            if ($PSCmdlet.ShouldProcess("Kestrun server '$($Server.ApplicationName)'", 'Clear custom PowerShell error response script')) {
                $Server.PowerShellErrorResponseScript = $null
            }
            return
        }

        if ($PSCmdlet.ShouldProcess("Kestrun server '$($Server.ApplicationName)'", 'Set custom PowerShell error response script')) {
            $Server.PowerShellErrorResponseScript = $ScriptBlock.ToString()
        }
    }
}

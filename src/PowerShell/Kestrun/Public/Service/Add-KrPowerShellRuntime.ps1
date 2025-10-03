<#
    .SYNOPSIS
        Adds PowerShell runtime support to the Kestrun server.
    .DESCRIPTION
        This cmdlet allows you to register a PowerShell runtime with the Kestrun server.
        It can be used to execute PowerShell scripts and commands in the context of the Kestrun server.
    .PARAMETER Server
        The Kestrun server instance to which the PowerShell runtime will be added.
    .PARAMETER PassThru
        If specified, the cmdlet will return the modified server instance.
    .EXAMPLE
        $server | Add-KrPowerShellRuntime -PathPrefix '/ps'
        This example adds PowerShell runtime support to the server, with a path prefix of '/ps'.
    .EXAMPLE
        $server | Add-KrPowerShellRuntime
        This example adds PowerShell runtime support to the server without a path prefix.
    .NOTES
        This cmdlet is used to register a PowerShell runtime with the Kestrun server, allowing you to execute PowerShell scripts and commands in the context of the Kestrun server.
#>
function Add-KrPowerShellRuntime {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {

        $Server.AddPowerShellRuntime() | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}


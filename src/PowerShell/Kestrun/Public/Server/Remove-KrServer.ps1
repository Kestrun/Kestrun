<#
    .SYNOPSIS
        Removes a Kestrun server instance.
    .DESCRIPTION
        This function stops and destroys a Kestrun server instance with the specified name.
    .PARAMETER Name
        The name of the Kestrun server instance to remove.
    .PARAMETER Force
        If specified, the server will be stopped and destroyed without confirmation.
    .EXAMPLE
        Remove-KrServer -Name "MyKestrunServer"
        Removes the specified Kestrun server instance.
    .EXAMPLE
        Remove-KrServer -Name "MyKestrunServer" -Force
        Removes the specified Kestrun server instance without confirmation.
    .NOTES
        This function is designed to be used in the context of a Kestrun server management.
#>
function Remove-KrServer {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Name,
        [Parameter()]
        [switch]$Force
    )
    process {
        if ( [Kestrun.KestrunHostManager]::Contains($Name) ) {
            if ($Force) {
                if ([Kestrun.KestrunHostManager]::IsRunning($Name)) {
                    [Kestrun.KestrunHostManager]::Stop($Name)
                }
                [Kestrun.KestrunHostManager]::Destroy($Name)
            } else {
                $confirm = Read-Host "Server '$Name' is running. Do you want to stop and destroy the previous instance? (Y/N)"
                if ($confirm -notin @('Y', 'y')) {
                    Write-Warning 'Operation cancelled by user.'
                    exit 1
                }
                if ([Kestrun.KestrunHostManager]::IsRunning($Name)) {
                    [Kestrun.KestrunHostManager]::Stop($Name)
                }
                [Kestrun.KestrunHostManager]::Destroy($Name)
            }
        }
    }
}


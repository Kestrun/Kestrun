<#
    .SYNOPSIS
        Creates a new Kestrun server instance.
    .DESCRIPTION
        This function initializes a new Kestrun server instance with the specified name and logger.
    .PARAMETER Name
        The name of the Kestrun server instance to create.
    .PARAMETER Logger
        An optional Serilog logger instance to use for logging.
    .PARAMETER PassThru
        If specified, the cmdlet will return the created server instance.
    .PARAMETER Force
        If specified, the cmdlet will overwrite any existing server instance with the same name.
    .EXAMPLE
        New-KrServer -Name "MyKestrunServer"
        Creates a new Kestrun server instance with the specified name.
    .NOTES
        This function is designed to be used in the context of a Kestrun server setup.
#>
function New-KrServer {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Name,
        [Parameter()]
        [Serilog.ILogger]$Logger = [Serilog.Log]::Logger,
        [Parameter()]
        [switch]$PassThru,
        [Parameter()]
        [switch]$Force
    )
    process {
        $loadedModules = Get-KrUserImportedModule
        $modulePaths = @($loadedModules | ForEach-Object { $_.Path })
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
        $server = [Kestrun.KestrunHostManager]::Create($Name, $Logger, [string[]] $modulePaths)
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}


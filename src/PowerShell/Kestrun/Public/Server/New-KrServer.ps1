<#
    .SYNOPSIS
        Creates a new Kestrun server instance.
    .DESCRIPTION
        This function initializes a new Kestrun server instance with the specified name and logger.
    .PARAMETER Name
        The name of the Kestrun server instance to create.
    .PARAMETER Logger
        An optional Serilog logger instance to use for logging.
        It's mutually exclusive with the LoggerName parameter.
        If not specified, the default logger will be used.
    .PARAMETER LoggerName
        An optional name of a registered logger to use for logging.
        It's mutually exclusive with the Logger parameter.
        If specified, the logger with this name will be used instead of the default logger.
    .PARAMETER DisablePowershellMiddleware
        If specified, the PowerShell middleware will be disabled for this server instance.
    .PARAMETER Default
        If specified, this server instance will be set as the default instance.
    .PARAMETER PassThru
        If specified, the cmdlet will return the created server instance.
    .PARAMETER Environment
        The environment to set for the Kestrun server instance. Valid values are 'Auto', 'Development', 'Staging', and 'Production'.
        - 'Auto' (default): Automatically sets the environment to 'Development' if a debugger is attached or
            if the -Debug switch is used; otherwise, it uses the environment specified by the KESTRUN_ENVIRONMENT environment variable
            or defaults to 'Production'.
        - 'Development': Forces the environment to 'Development'.
        - 'Staging': Forces the environment to 'Staging'.
        - 'Production': Forces the environment to 'Production'.
        The environment setting affects middleware behavior, such as detailed error pages in 'Development'.
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
    [CmdletBinding(DefaultParameterSetName = 'Logger')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [string]$Name,
        [Parameter(Mandatory = $false, ParameterSetName = 'Logger')]
        [Serilog.ILogger]$Logger,
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerName')]
        [string]$LoggerName,
        [Parameter()]
        [switch]$PassThru,
        [Parameter()]
        [switch]$DisablePowershellMiddleware,
        [Parameter()]
        [switch]$Default,
        [Parameter()]
        [ValidateSet('Auto', 'Development', 'Staging', 'Production')]
        [string]$Environment = 'Auto',
        [Parameter()]
        [switch]$Force
    )
    begin {
        # Honor explicit -Environment if provided
        if ($Environment -ne 'Auto') {
            Set-KrEnvironment -Name $Environment | Out-Null
        } else {
            # Auto: if debugger-ish, become Development; else clear override
            if (Test-KrDebugContext) {
                Set-KrEnvironment -Name Development | Out-Null
            } else {
                Set-KrEnvironment -Name Auto | Out-Null
            }

        }
        Write-Verbose ('Kestrun environment -> ' + (Get-KrDebugContext))
    }
    process {
        $loadedModules = Get-KrUserImportedModule
        $modulePaths = @($loadedModules | ForEach-Object { $_.Path })
        if ([Kestrun.KestrunHostManager]::Contains($Name) ) {
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

        # If Logger is not provided, use the default logger or the named logger
        if ($Null -eq $Logger) {
            if ([string]::IsNullOrEmpty($LoggerName)) {
                $Logger = [Serilog.Log]::Logger
            } else {
                # If LoggerName is specified, get the logger with that name
                $Logger = [Kestrun.Logging.LoggerManager]::Get($LoggerName)
            }
        }
        $enablePowershellMiddleware = -not $DisablePowershellMiddleware.IsPresent

        $server = [Kestrun.KestrunHostManager]::Create($Name, $Logger, [string[]] $modulePaths, $Default.IsPresent, $enablePowershellMiddleware)
        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

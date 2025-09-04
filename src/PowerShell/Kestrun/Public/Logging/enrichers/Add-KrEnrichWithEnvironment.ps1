<#
    .SYNOPSIS
        Adds environment information to the log context.
    .DESCRIPTION
        Adds environment information such as UserName and MachineName to the log context, allowing it to be included in log events.
    .PARAMETER LoggerConfig
        Instance of LoggerConfiguration
    .PARAMETER UserName
        If specified, enriches logs with the current user's name.
    .PARAMETER MachineName
        If specified, enriches logs with the current machine's name.
    .INPUTS
        None
    .OUTPUTS
        LoggerConfiguration object allowing method chaining
    .EXAMPLE
        PS> New-KrLogger | Add-KrEnrichWithEnvironment | Register-KrLogger
#>
function Add-KrEnrichWithEnvironment {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$loggerConfig,
        [Parameter(Mandatory = $false)]
        [switch]$UserName,
        [Parameter(Mandatory = $false)]
        [switch]$MachineName
    )

    process {
        $hasEnricher = $false

        # Only add if UserName is true or both are false (default: both on)
        if ($UserName -or (-not $UserName.IsPresent -and -not $MachineName.IsPresent)) {
            $loggerConfig = [Serilog.EnvironmentLoggerConfigurationExtensions]::WithEnvironmentUserName($loggerConfig.Enrich)
            $hasEnricher = $true
        }
        # Only add if MachineName is true or both are false (default: both on)
        if ($MachineName -or (-not $UserName.IsPresent -and -not $MachineName.IsPresent)) {
            $loggerConfig = [Serilog.EnvironmentLoggerConfigurationExtensions]::WithMachineName($loggerConfig.Enrich)
            $hasEnricher = $true
        }

        if (-not $hasEnricher) {
            Write-Verbose 'No environment enrichers added.'
        }

        return $loggerConfig
    }
}


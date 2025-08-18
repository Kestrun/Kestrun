<#
    .SYNOPSIS
        Sets the PowerShell script log level preferences based on the specified Serilog log level.
    .DESCRIPTION
        This function adjusts the PowerShell script log level preferences (Verbose, Debug, Information, Warning)
        based on the provided Serilog log level.
    .PARAMETER LogLevel
        The Serilog log level to set as the preference.
        Pass the Serilog log level that will be used to set the PowerShell script log level preferences.
    .EXAMPLE
        Set-KrLogLevelToPreference -LogLevel 'Error'
        # This will set the PowerShell script log level preferences to 'SilentlyContinue' for all levels above Error.
#>
function Set-KrLogLevelToPreference {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [Serilog.Events.LogEventLevel]$LogLevel
    )

    if ($PSCmdlet.ShouldProcess('Set log level preferences')) {
        if ([int]$LogLevel -le [int]([Serilog.Events.LogEventLevel]::Verbose)) {
            Set-Variable VerbosePreference -Value 'Continue' -Scope Global
        } else {
            Set-Variable VerbosePreference -Value 'SilentlyContinue' -Scope Global
        }

        if ([int]$LogLevel -le [int]([Serilog.Events.LogEventLevel]::Debug)) {
            Set-Variable DebugPreference -Value 'Continue' -Scope Global
        } else {
            Set-Variable DebugPreference -Value 'SilentlyContinue' -Scope Global
        }

        if ([int]$LogLevel -le [int]([Serilog.Events.LogEventLevel]::Information)) {
            Set-Variable InformationPreference -Value 'Continue' -Scope Global
        } else {
            Set-Variable InformationPreference -Value 'SilentlyContinue' -Scope Global
        }

        if ([int]$LogLevel -le [int]([Serilog.Events.LogEventLevel]::Warning)) {
            Set-Variable WarningPreference -Value 'Continue' -Scope Global
        } else {
            Set-Variable WarningPreference -Value 'SilentlyContinue' -Scope Global
        }
    }
}
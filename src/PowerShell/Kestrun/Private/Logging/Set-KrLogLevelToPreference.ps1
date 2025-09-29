<#
    .SYNOPSIS
        Sets the PowerShell script log level preferences based on the specified Serilog log level.
    .DESCRIPTION
        This function adjusts the PowerShell script log level preferences (Verbose, Debug, Information, Warning)
        based on the provided Serilog log level.
    .PARAMETER Level
        The Serilog log level to set as the preference.
        Pass the Serilog log level that will be used to set the PowerShell script log level preferences.
    .EXAMPLE
        Set-KrLogLevelToPreference -Level 'Error'
        # This will set the PowerShell script log level preferences to 'SilentlyContinue' for all levels above Error.
#>
function Set-KrLogLevelToPreference {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [Serilog.Events.LogEventLevel]$Level
    )

    if ($PSCmdlet.ShouldProcess('Set log level preferences')) {
        if ([int]$Level -le [int]([Serilog.Events.LogEventLevel]::Verbose)) {
            Set-Variable VerbosePreference -Values 'Continue' -Scope Global
        } else {
            Set-Variable VerbosePreference -Values 'SilentlyContinue' -Scope Global
        }

        if ([int]$Level -le [int]([Serilog.Events.LogEventLevel]::Debug)) {
            Set-Variable DebugPreference -Values 'Continue' -Scope Global
        } else {
            Set-Variable DebugPreference -Values 'SilentlyContinue' -Scope Global
        }

        if ([int]$Level -le [int]([Serilog.Events.LogEventLevel]::Information)) {
            Set-Variable InformationPreference -Values 'Continue' -Scope Global
        } else {
            Set-Variable InformationPreference -Values 'SilentlyContinue' -Scope Global
        }

        if ([int]$Level -le [int]([Serilog.Events.LogEventLevel]::Warning)) {
            Set-Variable WarningPreference -Values 'Continue' -Scope Global
        } else {
            Set-Variable WarningPreference -Values 'SilentlyContinue' -Scope Global
        }
    }
}


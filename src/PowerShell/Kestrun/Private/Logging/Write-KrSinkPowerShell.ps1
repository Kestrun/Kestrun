<#
    .SYNOPSIS
        Writes log events to the PowerShell host.
    .DESCRIPTION
        This function takes a log event and its rendered message, and writes it to the appropriate PowerShell host output stream.
    .PARAMETER LogEvent
        The log event to write.
    .PARAMETER RenderedMessage
        The rendered message of the log event.
#>
function Write-KrSinkPowerShell {
    param(
        [Parameter(Mandatory = $true)]
        [Serilog.Events.LogEvent]$LogEvent,
        [Parameter(Mandatory = $true)]
        [string]$RenderedMessage
    )

    switch ($LogEvent.Level) {
        Verbose {
            Write-Verbose -Message $RenderedMessage
        }
        Debug {
            Write-Debug -Message $RenderedMessage
        }
        Information {
            Write-Information -MessageData $RenderedMessage
        }
        Warning {
            Write-Warning -Message $RenderedMessage
        }
        default {
            Write-Information -MessageData $RenderedMessage -InformationAction 'Continue'
        }
    }
}


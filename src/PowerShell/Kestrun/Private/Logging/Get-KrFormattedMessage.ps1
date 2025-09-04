<#
    .SYNOPSIS
        Formats a log message for the specified logger and log level.
    .DESCRIPTION
        This function takes a log message and its parameters, and formats it for the specified logger and log level.
    .PARAMETER Logger
        The Serilog logger instance to use for formatting the message.
        Pass the logger instance that will be used to format the log message.
    .PARAMETER LogLevel
        The log level to use for the log message.
        Pass the log level that will be used to format the log message.
    .PARAMETER Message
        The log message to format.
        Pass the log message that will be formatted for the specified logger and log level.
    .PARAMETER Values
        An array of values to use for formatting the log message.
        Pass the values that will be used to format the log message.
    .PARAMETER Exception
        The exception to include in the log message, if any.
        Pass the exception that will be included in the log message.
    .EXAMPLE
        $formattedMessage = Get-KrFormattedMessage -Logger $logger -LogLevel 'Error' -Message 'An error occurred: {ErrorMessage}' -Values @{'ErrorMessage' = $errorMessage} -Exception $exception
        $formattedMessage | Write-Host
        # Output the formatted message
        Write-Host $formattedMessage
#>
function Get-KrFormattedMessage {
    param(
        [Parameter(Mandatory = $true)]
        [Serilog.ILogger]$Logger,

        [Parameter(Mandatory = $true)]
        [Serilog.Events.LogEventLevel]$LogLevel,

        [parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Message,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object[]]$Values,

        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [System.Exception]$Exception
    )

    $parsedTemplate = $null
    $boundProperties = $null
    if ($Logger.BindMessage($Message, $Values, [ref]$parsedTemplate, [ref]$boundProperties)) {
        $logEvent = [Serilog.Events.LogEvent]::new([System.DateTimeOffset]::Now, $LogLevel, $Exception, $parsedTemplate, $boundProperties)
        $strWriter = [System.IO.StringWriter]::new()
        # Use the global TextFormatter if available, otherwise use the default formatter from Kestrun.Logging
        [Kestrun.Logging]::TextFormatter.Format($logEvent, $strWriter)
        $message = $strWriter.ToString()
        $strWriter.Dispose()
        $message
    }
}


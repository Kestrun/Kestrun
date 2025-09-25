<#
    .SYNOPSIS
        Logs a message with the specified log level and parameters.
    .DESCRIPTION
        This function logs a message using the specified log level and parameters.
        It supports various log levels and can output the formatted message to the pipeline if requested.
    .PARAMETER Level
        The log level to use for the log event.
    .PARAMETER Message
        The message template describing the event.
    .PARAMETER LoggerName
        The name of the logger to use. If not specified, the default logger is used.
    .PARAMETER Logger
        The Serilog logger instance to use for logging.
        If not specified, the logger with the specified LoggerName or the default logger is used.
    .PARAMETER Exception
        The exception related to the event.
    .PARAMETER ErrorRecord
        The error record related to the event.
    .PARAMETER PropertyValues
        Objects positionally formatted into the message template.
    .PARAMETER PassThru
        If specified, outputs the formatted message into the pipeline.
    .INPUTS
        Message - Message template describing the event.
    .OUTPUTS
        None or Message populated with Properties into pipeline if PassThru specified.
    .EXAMPLE
        PS> Write-KrLog -Level Information -Message 'Info log message
        This example logs a simple information message.
    .EXAMPLE
        PS> Write-KrLog -Level Warning -Message 'Processed {@Position} in {Elapsed:000} ms.' -PropertyValues $position, $elapsedMs
        This example logs a warning message with formatted properties.
    .EXAMPLE
        PS> Write-KrLog -Level Error -Message 'Error occurred' -Exception ([System.Exception]::new('Some exception'))
        This example logs an error message with an exception.
    .NOTES
        This function is part of the Kestrun logging framework and is used to log messages at various levels.
        It can be used in scripts and modules that utilize Kestrun for logging.
#>
function Write-KrLog {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'LoggerName_MsgTemp')]
    param(
        [Parameter(Mandatory = $true)]
        [Serilog.Events.LogEventLevel]$Level,

        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerName_MsgTemp')]
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerName_ErrRec')]
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerName_Exception')]
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerManager_MsgTemp')]
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerManager_ErrRec')]
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerManager_Exception')]
        [AllowEmptyString()]
        [AllowNull()]
        [string]$Message,
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerName_MsgTemp')]
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerName_ErrRec')]
        [Parameter(Mandatory = $false, ParameterSetName = 'LoggerName_Exception')]
        [string]$LoggerName,
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerManager_MsgTemp')]
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerManager_ErrRec')]
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerManager_Exception')]
        [Serilog.Core.Logger]$Logger,
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerManager_Exception')]
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerName_Exception')]
        [AllowNull()]
        [System.Exception]$Exception,
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerManager_ErrRec')]
        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerName_ErrRec')]
        [AllowNull()]
        [System.Management.Automation.ErrorRecord]$ErrorRecord,
        [Parameter(Mandatory = $false)]
        [AllowNull()]
        [object[]]$PropertyValues,
        [Parameter(Mandatory = $false)]
        [switch]$PassThru
    )
    process {
        try {
            # If ErrorRecord is available wrap it into RuntimeException
            if ($null -ne $ErrorRecord) {

                if ($null -eq $Exception) {
                    # If Exception is not provided, use the ErrorRecord's Exception
                    $Exception = $ErrorRecord.Exception
                }

                $Exception = [Kestrun.Logging.Exceptions.WrapperException]::new($Exception, $ErrorRecord)
            }
            if ($Null -eq $Logger) {
                if ([string]::IsNullOrEmpty($LoggerName)) {
                    # If LoggerName is not specified, use the default logger
                    if ([Kestrun.Logging.LoggerManager]::DefaultLogger.GetType().FullName -eq 'Serilog.Core.Pipeline.SilentLogger') {
                        # If the default logger is a SilentLogger, it means no logger is configured
                        return
                    }
                    $Logger = [Kestrun.Logging.LoggerManager]::DefaultLogger
                } else {
                    # If LoggerName is specified, get the logger with that name
                    $Logger = [Kestrun.Logging.LoggerManager]::Get($LoggerName)
                }
            }
            # If Logger is not found, throw an error
            # This ensures that the logger is registered before logging
            if ($null -eq $Logger) {
                throw "Logger with name '$LoggerName' not found. Please ensure it is registered before logging."
            }
            # Log the message using the specified log level and parameters
            switch ($Level) {
                Verbose {
                    $Logger.Verbose($Exception, $Message, $PropertyValues)
                }
                Debug {
                    $Logger.Debug($Exception, $Message, $PropertyValues)
                }
                Information {
                    $Logger.Information($Exception, $Message, $PropertyValues)
                }
                Warning {
                    $Logger.Warning($Exception, $Message, $PropertyValues)
                }
                Error {
                    $Logger.Error($Exception, $Message, $PropertyValues)
                }
                Fatal {
                    $Logger.Fatal($Exception, $Message, $PropertyValues)
                }
            }
            # If PassThru is specified, output the formatted message into the pipeline
            # This allows the caller to capture the formatted message if needed
            if ($PassThru) {
                Get-KrFormattedMessage -Logger $Logger -Level $Level -Message $Message -PropertyValues $PropertyValues -Exception $Exception
            }
        } catch {
            # If an error occurs while logging, write to the default logger
            $defaultLogger = [Kestrun.Logging.LoggerManager]::DefaultLogger
            if ($null -ne $defaultLogger) {
                $defaultLogger.Error($_, 'Error while logging message: {Message}', $Message)
            } else {
                Write-Error "Error while logging message: $_"
            }
            throw $_
        }
    }
}

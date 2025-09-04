<#
    .SYNOPSIS
        Write an error log message using the Kestrun logging framework.
    .DESCRIPTION
        This function writes an error log message to the specified logger or the default logger.
    .PARAMETER Message
        The message template to log. This can include placeholders for properties.
    .PARAMETER Name
        The name of the log entry.
    .PARAMETER Logger
        The logger to use for logging. If not specified, the default logger is used.
    .PARAMETER Exception
        An optional exception to log along with the message.
    .PARAMETER ErrorRecord
        An optional error record to log. If provided, it will be logged as a fatal error.
    .PARAMETER Values
        An array of property values to include in the log message.
    .PARAMETER PassThru
        If specified, the function will return the logger object after logging.
    .EXAMPLE
        Write-KrErrorLog -Message "Error occurred: {0}" -Values "Some error"
        This example logs an error message with a property value.
    .EXAMPLE
        Write-KrErrorLog -Message "An error occurred" -Exception $exception -Logger $myLogger
        This example logs an error message with an exception using a specific logger.
    .EXAMPLE
        Write-KrErrorLog -Message "Fatal error" -ErrorRecord $errorRecord
        This example logs a fatal error message using an error record.
    .NOTES
        This function is part of the Kestrun logging framework and is used to log error messages.
        It can be used in scripts and modules that utilize Kestrun for logging.
#>
function Write-KrErrorLog {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'MsgTemp')]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ParameterSetName = 'MsgTemp')]
        [Parameter(Mandatory = $false, Position = 0, ValueFromPipeline = $true, ParameterSetName = 'ErrRec')]
        [AllowEmptyString()]
        [string]$Message,

        [Parameter(Mandatory = $false, ParameterSetName = 'MsgTemp')]
        [Parameter(Mandatory = $false, ParameterSetName = 'ErrRec')]
        [string]$Name,

        [Parameter(Mandatory = $false, ParameterSetName = 'MsgTemp')]
        [Parameter(Mandatory = $false, ParameterSetName = 'ErrRec')]
        [AllowNull()]
        [System.Exception]$Exception,

        [Parameter(Mandatory = $true, ParameterSetName = 'ErrRec')]
        [Alias('ER')]
        [AllowNull()]
        [System.Management.Automation.ErrorRecord]$ErrorRecord,

        [Parameter(Mandatory = $false, ParameterSetName = 'MsgTemp')]
        [Parameter(Mandatory = $false, ParameterSetName = 'ErrRec')]
        [AllowNull()]
        [object[]]$Values,

        [Parameter(Mandatory = $false, ParameterSetName = 'MsgTemp')]
        [Parameter(Mandatory = $false, ParameterSetName = 'ErrRec')]
        [switch]$PassThru
    )
    process {
        Write-KrLog -LogLevel Error -Name $Name -Message $Message -Exception $Exception -ErrorRecord $ErrorRecord -Values $Values -PassThru:$PassThru
    }
}


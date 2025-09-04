<#
    .SYNOPSIS
        Writes Fatal log message
    .DESCRIPTION
        Write a log event with the Fatal level.
    .PARAMETER Message
        Message template describing the event.
    .PARAMETER Name
        Name of the logger to use. If not specified, the default logger is used.
    .PARAMETER Exception
        Exception related to the event.
    .PARAMETER ErrorRecord
        ErrorRecord related to the event.
    .PARAMETER Values
        Optional property values to include in the log event.
    .PARAMETER PassThru
        Outputs Message populated with Values into pipeline.
    .INPUTS
        Message - Message template describing the event.
    .OUTPUTS
        None or Message populated with Values into pipeline if PassThru specified.
    .EXAMPLE
        PS> Write-KrFatalLog 'Fatal log message'
        This example logs a simple fatal message.
    .EXAMPLE
        PS> Write-KrFatalLog -Message 'Processed {@Position} in {Elapsed:000} ms.' -Values $position, $elapsedMs
        This example logs a fatal message with formatted properties.
    .EXAMPLE
        PS> Write-KrFatalLog 'Error occurred' -Exception ([System.Exception]::new('Some exception'))
        This example logs a fatal message with an exception.
    .NOTES
        This function is part of the Kestrun logging framework and is used to log fatal messages.
        It can be used in scripts and modules that utilize Kestrun for logging.
#>
function Write-KrFatalLog {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(DefaultParameterSetName = 'MsgTemp')]
    [OutputType([string])]
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
        Write-KrLog -LogLevel Fatal -Name $Name -Message $Message -Exception $Exception -ErrorRecord $ErrorRecord -Values $Values -PassThru:$PassThru
    }
}

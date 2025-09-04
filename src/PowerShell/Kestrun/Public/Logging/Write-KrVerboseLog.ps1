<#
    .SYNOPSIS
        Writes Verbose log message
    .DESCRIPTION
        Write a log event with the Verbose level.
    .PARAMETER Message
        Message template describing the event.
    .PARAMETER Name
        Name of the logger to use. If not specified, the default logger is used.
    .PARAMETER Exception
        Exception related to the event.
    .PARAMETER ErrorRecord
        ErrorRecord related to the event.
    .PARAMETER Values
        Objects positionally formatted into the message template.
    .PARAMETER PassThru
        Outputs Message populated with Values into pipeline.
    .INPUTS
        Message - Message template describing the event.
    .OUTPUTS
        None or Message populated with Values into pipeline if PassThru specified.
    .EXAMPLE
        PS> Write-KrVerboseLog 'Verbose log message' -Values 'value1', 'value2'
        This example logs a verbose message with two property values.
    .EXAMPLE
        PS> Write-KrVerboseLog -Message 'Processed {@Position} in {Elapsed:000} ms.' -Values $position, $elapsedMs
        This example logs a verbose message with formatted properties.
    .EXAMPLE
        PS> Write-KrVerboseLog 'Error occurred' -Exception ([System.Exception]::new('Some exception'))
        This example logs a verbose message with an exception.
    .NOTES
        This function is part of the Kestrun logging framework and is used to log verbose messages.
        It can be used in scripts and modules that utilize Kestrun for logging.
#>
function Write-KrVerboseLog {
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
        # Call the generic logging function with Verbose level
        Write-KrLog -LogLevel Verbose -Name $Name -Message $Message -Exception $Exception -ErrorRecord $ErrorRecord -Values $Values -PassThru:$PassThru
    }
}

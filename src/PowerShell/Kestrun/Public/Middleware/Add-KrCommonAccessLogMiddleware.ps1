<#
    .SYNOPSIS
        Adds Apache style common access logging to the Kestrun server.
    .DESCRIPTION
        Configures the Common Access Log middleware which emits request logs formatted like the
        Apache HTTPD common/combined log. The logs are written via the active Serilog pipeline so
        any configured sinks receive the access log entries.
    .PARAMETER Server
        The target Kestrun server instance. When omitted the current server is resolved automatically.
    .PARAMETER Level
        The Serilog log level used when emitting access log entries. Defaults to Information.
    .PARAMETER Logger
        The Serilog logger instance that should receive the access log entries. When not supplied the
        middleware uses the application's default logger from dependency injection.
        This parameter is mutually exclusive with LoggerName.
    .PARAMETER LoggerName
        The name of a registered logger that should receive the access log entries. When supplied
        the logger with this name is used instead of the default application logger.
        This parameter is mutually exclusive with Logger.
    .PARAMETER ExcludeQueryString
        Indicates whether the request query string should be excluded from the logged request line. Defaults to $true.
    .PARAMETER ExcludeProtocol
        Indicates whether the request protocol (for example HTTP/1.1) should be excluded from the logged request line. Defaults to $false.
    .PARAMETER IncludeElapsedMilliseconds
        Appends the total request duration in milliseconds to the access log entry when set to $true. Defaults to $false.
    .PARAMETER UseUtcTimestamp
        When specified the timestamp in the log entry is written in UTC instead of local server time.
    .PARAMETER TimestampFormat
        Optional custom timestamp format string. When omitted the Apache default "dd/MMM/yyyy:HH:mm:ss zzz" is used.
    .PARAMETER ClientAddressHeader
        Optional HTTP header name that contains the original client IP (for example X-Forwarded-For).
        When supplied the first value from the header is used instead of the socket address.
    .PARAMETER PassThru
        Returns the server instance to enable fluent pipelines when specified.
    .EXAMPLE
        Add-KrCommonAccessLogMiddleware -LoggerName 'myLogger' -UseUtcTimestamp

        Adds the Common Access Log middleware to the current Kestrun server using the named logger 'myLogger'
        and configures it to log timestamps in UTC.
    .EXAMPLE
        $server = New-KrServer -Name "My Server" |
            Add-KrListener -Port 8080 -IPAddress ([IPAddress]::Any) |
            Add-KrCommonAccessLogMiddleware -LoggerName 'myLogger' -PassThru

        Creates a new Kestrun server instance, adds a listener on port 8080 and the PowerShell runtime,
        then adds the Common Access Log middleware using the named logger 'myLogger' and returns the
        server instance in the $server variable.
#>
function Add-KrCommonAccessLogMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Logger')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [Serilog.Events.LogEventLevel]$Level = [Serilog.Events.LogEventLevel]::Information,

        [Parameter(Mandatory = $false, ParameterSetName = 'Logger')]
        [Serilog.ILogger]$Logger,

        [Parameter(Mandatory = $true, ParameterSetName = 'LoggerName')]
        [string]$LoggerName,

        [Parameter()]
        [switch]$ExcludeQueryString,

        [Parameter()]
        [switch]$ExcludeProtocol,

        [Parameter()]
        [switch]$IncludeElapsedMilliseconds,

        [Parameter()]
        [switch]$UseUtcTimestamp,

        [Parameter()]
        [string]$TimestampFormat,

        [Parameter()]
        [string]$ClientAddressHeader,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server

        # If Logger is not provided, use the default logger or the named logger
        if ($Null -eq $Logger) {
            if ([string]::IsNullOrEmpty($LoggerName)) {
                $Logger = [Serilog.Log]::Logger
            } else {
                # If LoggerName is specified, get the logger with that name
                $Logger = [Kestrun.Logging.LoggerManager]::Get($LoggerName)
            }
        }
    }
    process {

        $timestampFormatSet = $PSBoundParameters.ContainsKey('TimestampFormat')
        $clientHeaderSet = $PSBoundParameters.ContainsKey('ClientAddressHeader')

        $options = [Kestrun.Middleware.CommonAccessLogOptions]::new()
        $options.Level = $Level
        $options.IncludeQueryString = -not $ExcludeQueryString.IsPresent
        $options.IncludeProtocol = -not $ExcludeProtocol.IsPresent
        $options.IncludeElapsedMilliseconds = $IncludeElapsedMilliseconds.IsPresent
        $options.UseUtcTimestamp = $UseUtcTimestamp.IsPresent

        if ($timestampFormatSet) {
            $options.TimestampFormat = $TimestampFormat
        }

        if ($clientHeaderSet -and -not [string]::IsNullOrWhiteSpace($ClientAddressHeader)) {
            $options.ClientAddressHeader = $ClientAddressHeader
        }

        $options.Logger = $Logger

        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddCommonAccessLog($Server, $options) | Out-Null

        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}

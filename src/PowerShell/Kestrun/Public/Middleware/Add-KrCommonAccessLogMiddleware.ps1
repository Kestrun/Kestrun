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
    .PARAMETER IncludeQueryString
        Indicates whether the request query string should be included in the logged request line. Defaults to $true.
    .PARAMETER IncludeProtocol
        Indicates whether the HTTP protocol (for example HTTP/1.1) should be appended to the request line. Defaults to $true.
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
        $server | Add-KrCommonAccessLogMiddleware -ClientAddressHeader 'X-Forwarded-For'
        Adds the middleware and prefers the X-Forwarded-For header when reporting client addresses.
    .EXAMPLE
        $server | Add-KrCommonAccessLogMiddleware -IncludeElapsedMilliseconds -UseUtcTimestamp
        Emits access logs with execution time in milliseconds and timestamps written in UTC.
#>
function Add-KrCommonAccessLogMiddleware {
    [KestrunRuntimeApi('Definition')]
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [Serilog.Events.LogEventLevel]$Level = [Serilog.Events.LogEventLevel]::Information,

        [Parameter()]
        [bool]$IncludeQueryString = $true,

        [Parameter()]
        [bool]$IncludeProtocol = $true,

        [Parameter()]
        [bool]$IncludeElapsedMilliseconds = $false,

        [Parameter()]
        [bool]$UseUtcTimestamp = $false,

        [Parameter()]
        [string]$TimestampFormat,

        [Parameter()]
        [string]$ClientAddressHeader,

        [Parameter()]
        [switch]$PassThru
    )

    begin {
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }

        $timestampFormatSet = $PSBoundParameters.ContainsKey('TimestampFormat')
        $clientHeaderSet = $PSBoundParameters.ContainsKey('ClientAddressHeader')
    }

    process {
        $configure = [System.Action[Kestrun.Middleware.CommonAccessLogOptions]]{
            param($options)
            $options.Level = $Level
            $options.IncludeQueryString = $IncludeQueryString
            $options.IncludeProtocol = $IncludeProtocol
            $options.IncludeElapsedMilliseconds = $IncludeElapsedMilliseconds
            $options.UseUtcTimestamp = $UseUtcTimestamp

            if ($timestampFormatSet) {
                $options.TimestampFormat = $TimestampFormat
            }

            if ($clientHeaderSet -and -not [string]::IsNullOrWhiteSpace($ClientAddressHeader)) {
                $options.ClientAddressHeader = $ClientAddressHeader
            }
        }

        [Kestrun.Hosting.KestrunHttpMiddlewareExtensions]::AddCommonAccessLog($Server, $configure) | Out-Null

        if ($PassThru.IsPresent) {
            return $Server
        }
    }
}

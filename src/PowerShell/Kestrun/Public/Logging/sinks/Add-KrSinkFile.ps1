<#
    .SYNOPSIS
     Adds a file-based logging sink to the logging system.
    .DESCRIPTION
     The Add-KrSinkFile function configures a logging sink that writes log events to a specified file.
     It supports various options for file management, such as rolling intervals, file size limits, and custom output templates.
    .PARAMETER LoggerConfig
     The Serilog LoggerConfiguration object to which the file sink will be added.
    .PARAMETER Path
     The file path where log events will be written. This can include rolling file names.
    .PARAMETER Formatter
     An optional text formatter for custom log message formatting.
    .PARAMETER RestrictedToMinimumLevel
     The minimum log event level required to write to the file sink. Defaults to Verbose.
    .PARAMETER OutputTemplate
     The output template string for formatting log messages. Defaults to '{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{ErrorRecord}{Exception}'.
    .PARAMETER FormatProvider
     An optional format provider for customizing message formatting.
    .PARAMETER FileSizeLimitBytes
     The maximum size of the log file in bytes before it rolls over. Defaults to 1 GB.
    .PARAMETER LevelSwitch
     An optional LoggingLevelSwitch to dynamically control the logging level.
    .PARAMETER Buffered
     If set, log events are buffered before being written to the file. Defaults to false.
    .PARAMETER Shared
     If set, allows multiple processes to write to the same log file. Defaults to false.
    .PARAMETER FlushToDiskInterval
     The interval at which the log file is flushed to disk. Defaults to null (no periodic flushing).
    .PARAMETER RollingInterval
     The rolling interval for the log file. Defaults to Infinite (no rolling).
    .PARAMETER RollOnFileSizeLimit
     If set, the log file will roll over when it reaches the size limit, regardless of the rolling interval. Defaults to false.
    .PARAMETER RetainedFileCountLimit
     The maximum number of rolled log files to retain. Defaults to 31.
    .PARAMETER Encoding
     The encoding used for the log file. Defaults to null (system default).
    .PARAMETER Hooks
     Lifecycle hooks for managing the log file lifecycle. Defaults to null (no hooks).
    .EXAMPLE
     Add-KrSinkFile -LoggerConfig $config -Path "C:\Logs\app-.txt"
     Adds a file sink to the logging system that writes log events to "C:\Logs\app-.txt". The file name will roll over based on the specified rolling interval.
    .EXAMPLE
     Add-KrSinkFile -LoggerConfig $config -Path "C:\Logs\app-.txt" -Formatter $formatter
     Adds a file sink to the logging system that writes log events to "C:\Logs\app-.txt" using the specified text formatter.
    .EXAMPLE
     Add-KrSinkFile -LoggerConfig $config -Path "C:\Logs\app-.txt" -RollingInterval Day -RetainedFileCountLimit 7
     Adds a file sink that rolls over daily and retains the last 7 log files.
    .NOTES
     This function is part of the Kestrun logging infrastructure and should be used to enable file     logging.
#>
function Add-KrSinkFile {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([Serilog.LoggerConfiguration])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Serilog.LoggerConfiguration]$LoggerConfig,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true, ParameterSetName = 'Formatter')]
        [Serilog.Formatting.ITextFormatter]$Formatter,

        [Parameter(Mandatory = $false)]
        [Serilog.Events.LogEventLevel]$RestrictedToMinimumLevel = [Serilog.Events.LogEventLevel]::Verbose,

        [Parameter(Mandatory = $false)]
        [string]$OutputTemplate = '{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}',
        #= '{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{ErrorRecord}{Exception}',

        [Parameter(Mandatory = $false, ParameterSetName = 'Default')]
        [System.IFormatProvider]$FormatProvider = $null,

        [Parameter(Mandatory = $false)]
        [Nullable[long]]$FileSizeLimitBytes = [long]'1073741824',

        [Parameter(Mandatory = $false)]
        [Serilog.Core.LoggingLevelSwitch]$LevelSwitch = $null,

        [Parameter(Mandatory = $false)]
        [switch]$Buffered,

        [Parameter(Mandatory = $false)]
        [switch]$Shared,

        [Parameter(Mandatory = $false)]
        [Nullable[timespan]]$FlushToDiskInterval = $null,

        [Parameter(Mandatory = $false)]
        [Serilog.RollingInterval]$RollingInterval = [Serilog.RollingInterval]::Infinite,

        [Parameter(Mandatory = $false)]
        [switch]$RollOnFileSizeLimit,

        [Parameter(Mandatory = $false)]
        [Nullable[int]]$RetainedFileCountLimit = 31,

        [Parameter(Mandatory = $false)]
        [System.Text.Encoding]$Encoding = $null,

        [Parameter(Mandatory = $false)]
        [Serilog.Sinks.File.FileLifecycleHooks]$Hooks = $null
    )

    begin {
        $Path = $Path -replace '[\\/]', [IO.Path]::DirectorySeparatorChar
        $Path = Resolve-KrPath -Path $Path -KestrunRoot
        # Ensure directory exists before Serilog attempts to create/append the file
        try {
            $directory = [System.IO.Path]::GetDirectoryName($Path)
            if ($directory -and -not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
                [System.IO.Directory]::CreateDirectory($directory) | Out-Null
            }
        } catch {
            Write-Warning "Add-KrSinkFile: Failed to ensure log directory '$directory' exists. $_"
        }
    }
    process {
        switch ($PSCmdlet.ParameterSetName) {
            'Default' {
                return [Serilog.FileLoggerConfigurationExtensions]::File($LoggerConfig.WriteTo,
                    $Path,
                    $RestrictedToMinimumLevel,
                    $OutputTemplate,
                    $FormatProvider,
                    $FileSizeLimitBytes,
                    $LevelSwitch,
                    $Buffered,
                    $Shared,
                    $FlushToDiskInterval,
                    $RollingInterval,
                    $RollOnFileSizeLimit,
                    $RetainedFileCountLimit,
                    $Encoding,
                    $Hooks
                )
            }
            'Formatter' {
                return [Serilog.FileLoggerConfigurationExtensions]::File($LoggerConfig.WriteTo,
                    $Formatter,
                    $Path,
                    $RestrictedToMinimumLevel,
                    $FileSizeLimitBytes,
                    $LevelSwitch,
                    $Buffered,
                    $Shared,
                    $FlushToDiskInterval,
                    $RollingInterval,
                    $RollOnFileSizeLimit,
                    $RetainedFileCountLimit,
                    $Encoding,
                    $Hooks
                )
            }
        }
    }
}

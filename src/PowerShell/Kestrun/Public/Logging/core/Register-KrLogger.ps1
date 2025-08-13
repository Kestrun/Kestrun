function Register-KrLogger {
	<#
	.SYNOPSIS
		Starts the Kestrun logger.
	.DESCRIPTION
		This function initializes the Kestrun logger with specified configurations.
	.PARAMETER LoggerConfig
		A Serilog logger configuration object to set up the logger.
	.PARAMETER MinimumLevel
		The minimum log level for the logger. Default is Information.
	.PARAMETER Console
		If specified, adds a console sink to the logger.
	.PARAMETER PowerShell
		If specified, adds a PowerShell sink to the logger.
	.PARAMETER FilePath
		The file path where logs will be written. If not specified, defaults to a predefined path
	.PARAMETER FileRollingInterval
		The rolling interval for the log file. Default is Infinite.
	.PARAMETER SetAsDefault
		If specified, sets the created logger as the default logger for Serilog.
	.PARAMETER PassThru
		If specified, returns the created logger object.
	.EXAMPLE
		Register-KrLogger -MinimumLevel Debug -Console -FilePath "C:\Logs\kestrun.log" -FileRollingInterval Day -SetAsDefault
		Initializes the Kestrun logger with Debug level, adds console and file sinks, sets the logger as default, and returns the logger object.
	.EXAMPLE
		Register-KrLogger -LoggerConfig $myLoggerConfig -SetAsDefault
		Initializes the Kestrun logger using a pre-configured Serilog logger configuration object and sets it as the default logger.
	#>

	[KestrunRuntimeApi('Everywhere')]
    [CmdletBinding(SupportsShouldProcess = $true)]
	[OutputType([Serilog.ILogger])]
	param(
		[Parameter(Mandatory = $true)]
		[string]$Name,
		[Parameter(Mandatory = $true, ValueFromPipeline = $true, ParameterSetName = 'Full')]
		[Serilog.LoggerConfiguration]$LoggerConfig,

		[Parameter(Mandatory = $false, ParameterSetName = 'Short')]
		[Serilog.Events.LogEventLevel]$MinimumLevel = [Serilog.Events.LogEventLevel]::Information,

		[Parameter(Mandatory = $false, ParameterSetName = 'Short')]
		[switch]$Console,

		[Parameter(Mandatory = $false, ParameterSetName = 'Short')]
		[switch]$PowerShell,

		[Parameter(Mandatory = $false, ParameterSetName = 'Short')]
		[string]$FilePath,

		[Parameter(Mandatory = $false, ParameterSetName = 'Short')]
		[Serilog.RollingInterval]$FileRollingInterval = [Serilog.RollingInterval]::Infinite,

		[Parameter(Mandatory = $false)]
		[switch]$SetAsDefault,

		[Parameter(Mandatory = $false)]
		[switch]$PassThru
	)

	process {
		if ($PSCmdlet.ShouldProcess($Name, "Register logger")) {
			switch ($PsCmdlet.ParameterSetName) {
				'Short' {
					$LoggerConfig = New-KrLogger | Set-KrMinimumLevel -Value $MinimumLevel

					# If file path was not passed we setup default console sink
					if ($PowerShell -or -not $PSBoundParameters.ContainsKey('FilePath')) {
						$LoggerConfig = $LoggerConfig | Add-KrSinkPowerShell
					}

					if ($PSBoundParameters.ContainsKey('Console')) {
						$LoggerConfig = $LoggerConfig | Add-KrSinkConsole
					}

					if ($PSBoundParameters.ContainsKey('FilePath')) {
						$LoggerConfig = $LoggerConfig | Add-KrSinkFile -Path $FilePath -RollingInterval $FileRollingInterval
					}
				}
			}
			$logger = [Kestrun.Logging.LoggerConfigurationExtensions]::Register($LoggerConfig,$Name, $SetAsDefault)
			if ($PassThru) {
				return $logger
			}
		}
	}
}

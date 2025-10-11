<#
    .SYNOPSIS
        Adds StackExchange.Redis distributed cache services to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use StackExchange.Redis as the distributed cache for session state.
    .PARAMETER Server
        The Kestrun server instance to configure. If not specified, the current server instance is used.
    .PARAMETER Options
        The Redis cache options to configure. If not specified, default options are used.
    .PARAMETER ConfigurationOptions
        The StackExchange.Redis configuration options to use. If not specified, the default configuration is used.
        This parameter is mutually exclusive with the Configuration parameter.
    .PARAMETER Configuration
        The configuration string to use for connecting to the Redis server. If not specified, the default configuration is used.
        This parameter is mutually exclusive with the ConfigurationOptions parameter.
    .PARAMETER InstanceName
        The instance name to use for the Redis cache. If not specified, the default instance name is used.
    .PARAMETER PassThru
        If specified, the cmdlet returns the modified server instance after configuration.
    .EXAMPLE
        Add-KrDistributedRedisCache -Server $myServer -Options $myRedisOptions
        Adds StackExchange.Redis distributed cache services to the specified Kestrun server with the provided options.
    .EXAMPLE
        Add-KrDistributedRedisCache -Configuration "localhost:6379" -InstanceName "MyApp"
        Configures StackExchange.Redis distributed cache with the specified configuration string and instance name.
    .EXAMPLE
        $configOptions = [StackExchange.Redis.ConfigurationOptions]::Parse("localhost:6379")
        $configOptions.AbortOnConnectFail = $false
        Add-KrDistributedRedisCache -ConfigurationOptions $configOptions
        Configures StackExchange.Redis distributed cache with the specified configuration options.
    .NOTES
        This cmdlet is part of the Kestrun PowerShell module and is used to configure StackExchange.Redis distributed cache for Kestrun servers.
    .LINK
        https://docs.kestrun.dev/docs/powershell/kestrun/middleware
 #>
function Add-KrDistributedRedisCache {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [StackExchange.Redis.ConfigurationOptions]$ConfigurationOptions,

        [Parameter(ParameterSetName = 'Items')]
        [string]$Configuration,

        [Parameter(ParameterSetName = 'Items')]
        [string]$InstanceName,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            # Build Redis cache options from individual parameters
            $Options = [Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions]::new()

            if ($PsBoundParameters.ContainsKey('ConfigurationOptions')) {
                $Options.ConfigurationOptions = $ConfigurationOptions
            }
            if ($PsBoundParameters.ContainsKey('Configuration')) {
                $Options.Configuration = $Configuration
            }
            if ($PsBoundParameters.ContainsKey('InstanceName')) {
                $Options.InstanceName = $InstanceName
            }
        }
        # Add StackExchange.Redis distributed cache to the Kestrun server
        [Kestrun.Hosting.KestrunHostSessionExtensions]::AddStackExchangeRedisCache($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

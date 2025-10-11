<#
    .SYNOPSIS
        Adds SQL Server distributed cache services to the Kestrun server.
    .DESCRIPTION
        Configures the Kestrun server to use SQL Server as the distributed cache for session state.
    .PARAMETER Server
        The Kestrun server instance to configure. If not specified, the current server instance is used.
    .PARAMETER Options
        The SQL Server cache options to configure. If not specified, default options are used.
    .PARAMETER ConnectionString
        The connection string to the SQL Server database. If not specified, the default connection string is used.
    .PARAMETER SchemaName
        The schema name to use for the SQL Server cache. If not specified, the default schema name is used.
    .PARAMETER TableName
        The table name to use for the SQL Server cache. If not specified, the default table name is used.
    .PARAMETER ExpiredItemsDeletionInterval
        The interval in seconds at which expired items will be deleted from the cache. If not specified, the default is 30 minutes.
    .PARAMETER DefaultSlidingExpiration
        The default sliding expiration in seconds for cache items. If not specified, the default is 20 minutes.
    .PARAMETER PassThru
        If specified, the cmdlet returns the modified server instance after configuration.
    .EXAMPLE
        Add-KrDistributedSqlServerCache -Server $myServer -Options $mySqlOptions
        Adds SQL Server distributed cache services to the specified Kestrun server with the provided options.
    .EXAMPLE
        Add-KrDistributedSqlServerCache -ConnectionString "Server=myServer;Database=myDB;User Id=myUser;Password=myPass;" -SchemaName "dbo" -TableName "MyCache"
        Configures SQL Server distributed cache with the specified connection string, schema name, and table name.
    .EXAMPLE
        Add-KrDistributedSqlServerCache -ExpiredItemsDeletionInterval 1800 -DefaultSlidingExpiration 1200
        Configures SQL Server distributed cache with an expired items deletion interval of 1800 seconds and a default sliding expiration of 1200 seconds.
    .NOTES
        This cmdlet is part of the Kestrun PowerShell module and is used to configure SQL Server distributed cache for Kestrun servers.
    .LINK
        https://docs.kestrun.dev/docs/powershell/kestrun/middleware
 #>
function Add-KrDistributedSqlServerCache {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Microsoft.Extensions.Caching.SqlServer.SqlServerCacheOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [string]$ConnectionString,

        [Parameter(ParameterSetName = 'Items')]
        [string]$SchemaName,

        [Parameter(ParameterSetName = 'Items')]
        [string]$TableName,

        [Parameter(ParameterSetName = 'Items')]
        [ValidateRange(1, [int]::MaxValue)]
        [int]$ExpiredItemsDeletionInterval,

        [Parameter(ParameterSetName = 'Items')]
        [ValidateRange(1, [int]::MaxValue)]
        [int]$DefaultSlidingExpiration,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            # Build SQL Server distributed cache options
            $Options = [Microsoft.Extensions.Caching.SqlServer.SqlServerCacheOptions]::new()

            if ($PsBoundParameters.ContainsKey('ConnectionString')) {
                $Options.ConnectionString = $ConnectionString
            }
            if ($PsBoundParameters.ContainsKey('SchemaName')) {
                $Options.SchemaName = $SchemaName
            }
            if ($PsBoundParameters.ContainsKey('TableName')) {
                $Options.TableName = $TableName
            }
            if ($PsBoundParameters.ContainsKey('ExpiredItemsDeletionInterval')) {
                $Options.ExpiredItemsDeletionInterval = [TimeSpan]::FromSeconds($ExpiredItemsDeletionInterval)
            }
            if ($PsBoundParameters.ContainsKey('DefaultSlidingExpiration')) {
                $Options.DefaultSlidingExpiration = [TimeSpan]::FromSeconds($DefaultSlidingExpiration)
            }
        }

        # Register the SQL Server distributed cache with the host
        [Kestrun.Hosting.KestrunHostSessionExtensions]::AddDistributedSqlServerCache($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

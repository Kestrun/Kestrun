<#
    .SYNOPSIS
        Maps a SignalR hub class to the given URL path.
    .DESCRIPTION
        This function allows you to map a SignalR hub class to a specific URL path on the Kestrun server.
    .PARAMETER Path
        The URL path where the SignalR hub will be accessible. Defaults to '/hubs/kestrun'.
    .PARAMETER DocId
        The OpenAPI document IDs to which the SignalR hub endpoint should be added. Default is '
    .PARAMETER Summary
        Optional OpenAPI summary override for the SignalR hub endpoint.
    .PARAMETER Description
        Optional OpenAPI description override for the SignalR hub endpoint.
    .PARAMETER Tags
        Optional OpenAPI tags override for the SignalR hub endpoint.
    .PARAMETER HubName
        Optional name of the SignalR hub. If not provided, defaults to 'kestrun'.
    .PARAMETER IncludeNegotiateEndpoint
        If specified, includes the negotiate endpoint for the SignalR hub.
    .PARAMETER SkipOpenApi
        If specified, the OpenAPI documentation for this endpoint will be skipped.
    .PARAMETER Options
        Full OpenAPI customization object (Kestrun.Hosting.Options.SignalROpenApiOptions).
        When provided, it takes precedence over the individual -OpenApi* parameters.
    .EXAMPLE
        Add-KrSignalRHubMiddleware -Path '/hubs/notifications' -HubName 'NotificationsHub' -IncludeNegotiateEndpoint
        Adds a SignalR hub at the path '/hubs/notifications' with the hub name 'NotificationsHub' and includes the negotiate endpoint.
    .EXAMPLE
        $options = [Kestrun.Hosting.Options.SignalROptions]::new()
        $options.Path = '/hubs/updates'
        $options.HubName = 'UpdatesHub'
        $options.IncludeNegotiateEndpoint = $true
        Add-KrSignalRHubMiddleware -Options $options
        Adds a SignalR hub at the path '/hubs/updates' with the hub name 'UpdatesHub' and includes the negotiate endpoint, using the provided options object for configuration.
    .EXAMPLE
        Add-KrSignalRHubMiddleware -Path '/hubs/alerts' -HubName 'AlertsHub' -SkipOpenApi
        Adds a SignalR hub at the path '/hubs/alerts' with the hub name 'AlertsHub' and skips OpenAPI documentation for this endpoint.
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage SignalR hubs on the Kestrun server.
        The Server parameter accepts a KestrunHost instance; if not provided, the default server is used.
        The Path parameter specifies the URL path where the SignalR hub will be accessible.
        The PassThru switch allows the function to return the modified server instance for further use.
#>
function Add-KrSignalRHubMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Items')]
    param(
        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [Parameter(Mandatory = $false, ParameterSetName = 'ItemsSkipOpenApi')]
        [string]$Path,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [Parameter(Mandatory = $false, ParameterSetName = 'ItemsSkipOpenApi')]
        [string[]]$DocId = [Kestrun.OpenApi.OpenApiDocDescriptor]::DefaultDocumentationIds,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string]$Summary,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string]$Description,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string[]]$Tags,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [string]$HubName,

        [Parameter(Mandatory = $false, ParameterSetName = 'ItemsSkipOpenApi')]
        [switch]$SkipOpenApi,

        [Parameter(Mandatory = $false, ParameterSetName = 'Items')]
        [switch]$IncludeNegotiateEndpoint,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Hosting.Options.SignalROptions]$Options
    )
    # Ensure the server instance is resolved
    $Server = Resolve-KestrunServer

    # If the Options parameter set is not used, create an options object from the individual parameters
    if ($PSCmdlet.ParameterSetName -eq 'Items') {
        $Options = [Kestrun.Hosting.Options.SignalROptions]::new()
        if ( $SkipOpenApi.IsPresent ) {
            $Options.SkipOpenApi = $true
        }
        # Set the OpenAPI documentation details for the SignalR hub endpoint
        if ($PSBoundParameters.ContainsKey('Summary')) { $Options.Summary = $Summary }
        if ($PSBoundParameters.ContainsKey('Description')) { $Options.Description = $Description }
        if ($PSBoundParameters.ContainsKey('Tags')) { $Options.Tags = $Tags }
        if ($PSBoundParameters.ContainsKey('HubName')) { $Options.HubName = $HubName }
        # Set the documentation IDs for the SignalR hub
        $Options.DocId = $DocId
        # Set the path for the SignalR hub
        if ($PSBoundParameters.ContainsKey('Path')) { $Options.Path = $Path }
        $Options.IncludeNegotiateEndpoint = $IncludeNegotiateEndpoint.IsPresent
    }
    # Add the SignalR hub middleware to the server
    $server.AddSignalR($Options) | Out-Null
}


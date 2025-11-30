<#
.SYNOPSIS
    Builds and adds the Map Route to the Kestrun server.
.DESCRIPTION
    The Build-KrMapRoute cmdlet builds the Map Route from the provided Map Route Builder and adds it to the specified Kestrun server.
.PARAMETER MapRouteBuilder
    The Map Route Builder object used to define the route mapping.
.PARAMETER Endpoints
    (Optional) An array of endpoint names to which the route will be mapped. If not provided, the route will be mapped to all available endpoints.
.PARAMETER AllowDuplicate
    A switch indicating whether to allow duplicate routes. If specified, duplicate routes will be allowed.
.PARAMETER DuplicateAction
    Specifies the action to take when a duplicate route is detected. Valid values are 'Throw', 'Skip', 'Allow', and 'Warn'. Default is 'Throw'.
.PARAMETER PassThru
    A switch indicating whether to return the modified server instance after adding the route.
.EXAMPLE
    # Create a new Map Route Builder
    New-KrMapRouteBuilder |
    Add-KrMapRouteVerbPattern -MapRouteBuilder $mapRouteBuilder -Verbs @('GET', 'POST') -Pattern '/api/items'|
    # Build and add the route to the server
    Build-KrMapRoute -AllowDuplicate -DuplicateAction 'Warn'
.NOTES
    This cmdlet is part of the route builder module.
#>
function Build-KrMapRoute {
    [KestrunRuntimeApi('Definition')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteBuilder]$MapRouteBuilder,
        [Parameter()]
        [string[]]$Endpoints,
        [Parameter()]
        [switch]$AllowDuplicate,
        [Parameter()]
        [ValidateSet('Throw', 'Skip', 'Allow', 'Warn')]
        [string]$DuplicateAction = 'Throw',
        [Parameter()]
        [switch]$PassThru
    )
    process {
        # Build and add the route to the server
        $params = @{
            Server = $MapRouteBuilder.Server
            Options = $MapRouteBuilder
            AllowDuplicate = $AllowDuplicate.IsPresent
            DuplicateAction = $DuplicateAction
        }

        if ($PSBoundParameters.ContainsKey('Endpoints')) {
            $params['Endpoints'] = $Endpoints
        }

        Add-KrMapRoute @params

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

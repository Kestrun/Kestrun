<#
    .SYNOPSIS
        Tests if a route exists in the Kestrun host.
    .DESCRIPTION
        This function checks if a specific route is defined in the Kestrun host's routing table.
    .PARAMETER Pattern
        The path of the route to test.
    .PARAMETER Verbs
        The HTTP verb(s) to test for the route.
    .EXAMPLE
        Test-KrRoute -Path "/api/test" -Verbs "GET"
        # Tests if a GET route exists for "/api/test".
    .EXAMPLE
        Test-KrRoute -Path "/api/test" -Verbs "POST"
        # Tests if a POST route exists for "/api/test".
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes.
#>
function Test-KrRoute {
    [KestrunRuntimeApi('Everywhere')]
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [alias('Path')]
        [string]$Pattern,

        [Parameter()]
        [Kestrun.Utilities.HttpVerb[]]$Verbs = @([Kestrun.Utilities.HttpVerb]::Get)
    )
    # Ensure the server instance is resolved
    $Server = Resolve-KestrunServer -Server $Server
    if ($null -eq $Server) {
        throw 'Server is not initialized. Please ensure the server is configured before setting options.'
    }

    return [Kestrun.Hosting.KestrunHostMapExtensions]::MapExists($Server, $Pattern, $Verbs)
}


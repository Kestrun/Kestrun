<#
.SYNOPSIS
    Loads OpenAPI-annotated PowerShell functions into a KestrunHost instance.
.DESCRIPTION
    The Get-KrAnnotatedFunctionsLoaded cmdlet scans all loaded PowerShell functions
    in the current runspace for OpenAPI annotations and loads them into the specified
    KestrunHost instance as API routes.
.PARAMETER Server
    The KestrunHost instance to load the annotated functions into. If not specified,
    the default KestrunHost instance will be used.
.PARAMETER DocId
    The ID of the OpenAPI document to build. Default is 'default'.
.EXAMPLE
    Get-KrAnnotatedFunctionsLoaded -Server $myKestrunHost
    Loads all OpenAPI-annotated functions into the specified KestrunHost instance.
.NOTES
    This cmdlet is designed to be used within the context of a KestrunHost instance.
#>
function Get-KrAnnotatedFunctionsLoaded {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,
        [Parameter()]
        [string]$DocId = [Kestrun.Authentication.IOpenApiAuthenticationOptions]::DefaultSchemeName
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        # All loaded functions now in the runspace
        $funcs = @(Get-Command -CommandType Function | Where-Object {
                $null -eq $_.Module -and $null -eq $_.PsDrive
            })
    }
    process {
        if ( -not $Server.OpenApiDocumentDescriptor.ContainsKey($DocId)) {
            throw "OpenAPI document with ID '$DocId' does not exist on the server."
        }
        $doc = $Server.OpenApiDocumentDescriptor[$DocId]
        $doc.LoadAnnotatedFunctions( $funcs )
    }
}

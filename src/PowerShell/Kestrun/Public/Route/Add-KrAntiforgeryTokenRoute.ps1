<#
.SYNOPSIS
    Adds a GET endpoint that issues the antiforgery cookie and returns a JSON token payload.
.DESCRIPTION
    Maps a token endpoint (default: /csrf-token) using the C# extension
    [Kestrun.Hosting.KestrunHostMapExtensions]::AddAntiforgeryTokenRoute().
    The endpoint is exempt from CSRF validation and responds with:
        { "token": "<RequestToken>", "headerName": "<ConfiguredHeaderOrNull>" }
.PARAMETER Server
    The Kestrun server instance (pipeline-friendly).
.PARAMETER Path
    Route path to expose. Defaults to "/csrf-token".
.PARAMETER PassThru
    Return the server instance for chaining.
.EXAMPLE
    $server | Add-KrAntiforgeryMiddleware -CookieName ".Kestrun.AntiXSRF" -HeaderName "X-CSRF-TOKEN" -PassThru |
      Add-KrAntiforgeryTokenRoute -Path "/csrf-token" -PassThru
.EXAMPLE
    # Client test (PowerShell):
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $info = Invoke-RestMethod "http://127.0.0.1:5000/csrf-token" -WebSession $session
    $hdr = $info.headerName ?? 'X-CSRF-TOKEN'
    Invoke-RestMethod "http://127.0.0.1:5000/profile" -Method Post -WebSession $session `
      -Headers @{ $hdr = $info.token } -ContentType 'application/json' -Body (@{name='Max'}|ConvertTo-Json)
#>
function Add-KrAntiforgeryTokenRoute {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost] $Server,

        [Parameter()]
        [ValidateNotNullOrEmpty()]
        [string] $Path = "/csrf-token",

        [Parameter()]
        [switch] $PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
        if (-not $Server) { throw "Server is not initialized. Call New-KrServer and Enable-KrConfiguration first." }
    }
    process {
        # Call the C# extension that maps the endpoint and disables antiforgery on it
        $null = [Kestrun.Hosting.KestrunHostMapExtensions]::AddAntiforgeryTokenRoute($Server, $Path)

        if ($PassThru) { return $Server }
    }
}

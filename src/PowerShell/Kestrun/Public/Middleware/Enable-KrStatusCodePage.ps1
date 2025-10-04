<#
    .SYNOPSIS
        Enables status code page handling for Kestrun apps.
    .DESCRIPTION
        Configures how the server responds to error HTTP status codes (400–599).
        Supports default text, redirect, re-execute, or a custom handler.
    .PARAMETER Server
        The Kestrun Server instance.
    .PARAMETER Mode
        Mode of operation: Default | Redirect | ReExecute | Custom
    .PARAMETER Path
        Path for Redirect/ReExecute (e.g. "/error/{0}")
    .PARAMETER ScriptBlock
        Custom handler scriptblock (receives the response object).
    .EXAMPLE
        Enable-KrStatusCodePages -Host $Server -Mode Default
    .EXAMPLE
        Enable-KrStatusCodePages -Host $Server -Mode Redirect -Path "/error/{0}"
    .EXAMPLE
        Enable-KrStatusCodePages -Host $Server -Mode ReExecute -Path "/error/{0}"
    .EXAMPLE
        Enable-KrStatusCodePages -Host $Server -Mode Custom -ScriptBlock {
            param($ctx)
            $ctx.Response.ContentType = "application/json"
            $body = @{ error = "Oops"; status = $ctx.Response.StatusCode }
            [System.Text.Json.JsonSerializer]::Serialize($body) |
                % { [System.IO.StreamWriter]::new($ctx.Response.Body).Write($_) }
        }
    #>
function Enable-KrStatusCodePage {
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [ValidateSet('Default', 'Redirect', 'ReExecute', 'Custom')]
        [string]$Mode = 'Default',

        [Parameter(ParameterSetName = 'Path')]
        [string]$Path,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $false)]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        switch ($Mode) {
            'Default' { [Kestrun.Extensions.StatusCodePagesExtensions]::UseDefault($Server) }
            'Redirect' { [Kestrun.Extensions.StatusCodePagesExtensions]::UseRedirect($Server, $Path) }
            'ReExecute' { [Kestrun.Extensions.StatusCodePagesExtensions]::UseReExecute($Server, $Path) }
            'Custom' {
                if (-not $ScriptBlock) { throw 'Custom mode requires -ScriptBlock' }
                [Kestrun.Extensions.StatusCodePagesExtensions]::UseCustom($Server, $ScriptBlock)
            }
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}

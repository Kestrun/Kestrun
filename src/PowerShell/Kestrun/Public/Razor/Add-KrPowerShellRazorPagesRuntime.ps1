<#
.SYNOPSIS
    Adds PowerShell support for Razor Pages.
.DESCRIPTION
    This cmdlet allows you to register Razor Pages with PowerShell support in the Kestrun server.
    It can be used to serve dynamic web pages using Razor syntax with PowerShell code blocks.
.PARAMETER Server
    The Kestrun server instance to which the PowerShell Razor Pages service will be added.
.PARAMETER RootPath
    The root directory for the Razor Pages. If not specified, the default 'Pages' directory under the content root will be used.
.PARAMETER PathPrefix
    An optional path prefix for the Razor Pages. If specified, the Razor Pages will be served under this path.
.PARAMETER PassThru
    If specified, the cmdlet will return the modified server instance.
.EXAMPLE
    $server | Add-KrPowerShellRazorPagesRuntime -PathPrefix '/pages'
    This example adds PowerShell support for Razor Pages to the server, with a path prefix of '/pages'.
.EXAMPLE
    $server | Add-KrPowerShellRazorPagesRuntime
    This example adds PowerShell support for Razor Pages to the server without a path prefix.
.NOTES
    This cmdlet is used to register Razor Pages with PowerShell support in the Kestrun server, allowing you to serve dynamic web pages using Razor syntax with PowerShell code blocks.
#>
function Add-KrPowerShellRazorPagesRuntime {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter()]
        [string]$RootPath = $null,

        [Parameter()]
        [string]$PathPrefix = $null,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if (-not ([string]::IsNullOrEmpty($RootPath))) {
            $RootPath = (Resolve-KrPath -Path $RootPath)
        }
        # Add PowerShell support for Razor Pages with a path prefix
        $Server.AddPowerShellRazorPages($Server, $RootPath, $PathPrefix) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the modified server instance
            return $Server
        }
    }
}


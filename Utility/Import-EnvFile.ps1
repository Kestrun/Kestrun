<#
.SYNOPSIS
  Import top-level properties from a JSON file into environment variables.

.DESCRIPTION
  - Reads a JSON file whose top-level object contains simple properties (no deep nesting).
  - Sets environment variables with the same property names and string values.
  - Default file path is './env.json'.
  - Default scope is Process (use 'User' to persist for current user).
  - WARNING: If you run this script normally, it sets variables for the script process (and its children).
             To set variables in your current shell session, dot-source this script:
               . .\Import-EnvJson.ps1 -Path .\env.json
.PARAMETER Path
  The path to the JSON file to import. Default is './.vscode/env.json'.
.PARAMETER Scope
  The scope to set the environment variables in. Default is 'Process'.
  Use 'User' to persist for the current user, or 'Machine' for all users (requires admin).
.PARAMETER Overwrite
  If specified, existing environment variables will be overwritten. By default, existing variables are preserved.
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$Path = '.\.env.json',

    [ValidateSet('Process', 'User', 'Machine')]
    [string]$Scope = 'Process',

    [switch]$Overwrite,

    [switch]$FailOnMissingFile
)

if (-not (Test-Path -LiteralPath $Path)) {
    if ( $FailOnMissingFile.IsPresent) {
        Write-Error "JSON file not found: $Path"
        exit 2
    }
    exit 0
}

try {
    $jsonRaw = Get-Content -Raw -LiteralPath $Path -Encoding UTF8
    $obj = $jsonRaw | ConvertFrom-Json -ErrorAction Stop
} catch {
    Write-Error "Failed to read/parse JSON file '$Path': $_"
    exit 3
}

if ($null -eq $obj) {
    Write-Error "JSON parsed to null (empty file?): $Path"
    exit 4
}

# Only accept top-level object with properties
if (-not ($obj -is [PSCustomObject] -or $obj -is [hashtable])) {
    Write-Error 'Expected a JSON object with top-level properties. Arrays or primitives are not supported.'
    exit 5
}

# Convert to dictionary of key/value strings
$props = @{}
foreach ($p in $obj.PSObject.Properties) {
    $k = $p.Name
    $v = $p.Value

    if ($v -is [System.Management.Automation.PSCustomObject] -or $v -is [hashtable] -or ($v -is [System.Collections.IEnumerable] -and -not ($v -is [string]))) {
        Write-Error "Nested objects/arrays are not supported for key '$k'. Provide only top-level scalar/string values."
        exit 6
    }

    # Convert to string
    $props[$k] = [string]$v
}

# Set env vars
foreach ($k in $props.Keys) {
    $val = $props[$k]

    $existing = [System.Environment]::GetEnvironmentVariable($k, $Scope)
    if (-not $Overwrite.IsPresent -and $existing) {
        Write-Verbose "Skipping existing environment variable '$k' (use -Overwrite to force replacement)."
        continue
    }

    try {
        [System.Environment]::SetEnvironmentVariable($k, $val, $Scope)
    } catch {
        Write-Error "Failed to set environment variable '$k' (scope: $Scope): $_"
        exit 7
    }
}

# Success: exit 0

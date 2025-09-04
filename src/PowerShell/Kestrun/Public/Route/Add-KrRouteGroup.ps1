<#
    .SYNOPSIS
        Creates a grouped route context (prefix + shared options) for nested Add-KrMapRoute calls.
    .DESCRIPTION
        While the ScriptBlock runs, all Add-KrMapRoute calls inherit:
          - Prefix (prepended to -Path)
          - AuthorizationSchema / AuthorizationPolicy
          - ExtraImports / ExtraRefs
          - Arguments (merged; child overrides keys)
        Supports nesting; inner groups inherit and can override unless -NoInherit is used.
    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
        If not specified, the function will attempt to resolve the current server context.
    .PARAMETER Options
        The options to apply to all routes in the group.
    .PARAMETER Prefix
        The path prefix for the group (e.g. '/todoitems').
    .PARAMETER AuthorizationSchema
        Authorization schemes required by all routes in the group.
    .PARAMETER AuthorizationPolicy
        Authorization policies required by all routes in the group.
    .PARAMETER ExtraImports
        Extra namespaces added to all routes in the group.
    .PARAMETER ExtraRefs
        Extra assemblies referenced by all routes in the group.
    .PARAMETER Arguments
        Extra arguments injected into all routes in the group.
    .PARAMETER ScriptBlock
        ScriptBlock within which you call Add-KrMapRoute for relative paths.
    .PARAMETER FileName
        Path to a script file containing the scriptblock to execute.
    .PARAMETER NoInherit
        If set, do not inherit options from the parent group; only apply the current parameters.
    .PARAMETER PassThru
        If specified, the function will return the created route object.
    .EXAMPLE
        Add-KrRouteGroup -Prefix '/todoitems' -AuthorizationPolicy 'RequireUser' -ScriptBlock {
            Add-KrMapRoute -Verbs Get  -Path '/'      -ScriptBlock { 'all todos' }
            Add-KrMapRoute -Verbs Get  -Path '/{id}'  -ScriptBlock { "todo $($Context.Request.RouteValues['id'])" }
            Add-KrMapRoute -Verbs Post -Path '/'      -ScriptBlock { write-KrResponse -InputObject 'create' }
        }
        Adds a new route group to the specified Kestrun server with the given prefix and options.
    .EXAMPLE
        Add-KrRouteGroup -Prefix '/todoitems' -FileName 'C:\Scripts\TodoItems.ps1'
        Add the new route group defined in the specified file.
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes
#>
function Add-KrRouteGroup {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'ScriptBlock', PositionalBinding = $true)]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory, ParameterSetName = 'ScriptBlockWithOptions')]
        [Parameter(Mandatory, ParameterSetName = 'FileNameWithOptions')]
        [Kestrun.Hosting.Options.MapRouteOptions]$Options,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'FileName')]
        [string]$Prefix,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'FileName')]
        [string[]]$AuthorizationSchema,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'FileName')]
        [string[]]$AuthorizationPolicy,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'FileName')]
        [string[]]$ExtraImports,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'FileName')]
        [System.Reflection.Assembly[]]$ExtraRefs,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'FileName')]
        [hashtable]$Arguments,

        [Parameter(Mandatory, Position = 0, ParameterSetName = 'ScriptBlock')]
        [Parameter(Mandatory, Position = 0, ParameterSetName = 'ScriptBlockWithOptions')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory, ParameterSetName = 'FileName')]
        [Parameter(Mandatory, ParameterSetName = 'FileNameWithOptions')]
        [string]$FileName,

        [Parameter()]
        [switch]$NoInherit,

        [Parameter()]
        [switch]$PassThru
    )
    process {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server

        if ($PSCmdlet.ParameterSetName -like 'FileName*') {
            if (-not (Test-Path -Path $FileName)) {
                throw "The specified file path does not exist: $FileName"
            }
            $code = Get-Content -Path $FileName -Raw
            $ScriptBlock = [scriptblock]::Create($code)
        }

        # Normalize prefix: allow "todoitems" or "/todoitems"
        if (-not [string]::IsNullOrWhiteSpace($Prefix) -and -not $Prefix.StartsWith('/')) {
            $Prefix = "/$Prefix"
        }
        # Discover parent template (stack always aims to hold MapRouteOptions)
        $parentOpts = $null
        if (-not $NoInherit -and $server.RouteGroupStack.Count) {
            $parentOpts = $server.RouteGroupStack.Peek()
        }

        $curr = if ($PSCmdlet.ParameterSetName -like '*WithOptions') {
            $Options
        } else {
            $dict = $null
            if ($Arguments) {
                $dict = [System.Collections.Generic.Dictionary[string, object]]::new()
                foreach ($k in $Arguments.Keys) { $dict[$k] = $Arguments[$k] }
            }
            New-KrMapRouteOption -Property @{
                RequireSchemes = $AuthorizationSchema
                RequirePolicies = $AuthorizationPolicy
                ExtraImports = $ExtraImports
                ExtraRefs = $ExtraRefs
                Arguments = $dict
                Pattern = $Prefix
            }
        }
        # Merge with parent (parent first, then current overrides)
        if ($parentOpts) {
            $curr = _KrMerge-MRO -Parent $parentOpts -Child $curr
        }

        $Server.RouteGroupStack.Push($curr)
        try {
            & $ScriptBlock
        } catch {
            $msg = "Error inside route group '$($current.Prefix)': $($_.Exception.Message)"
            throw [System.Exception]::new($msg, $_.Exception)
        } finally {
            $null = $Server.RouteGroupStack.Pop()
            if ($restorePath) { Set-Location $restorePath }
        }

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}


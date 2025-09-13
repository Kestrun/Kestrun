<#
    .SYNOPSIS
        Adds a new map route to the Kestrun server.
    .DESCRIPTION
        This function allows you to add a new map route to the Kestrun server by specifying the route path and the script block or code to be executed when the route is accessed.
    .PARAMETER Server
        The Kestrun server instance to which the route will be added.
        If not specified, the function will attempt to resolve the current server context.
    .PARAMETER Options
        An instance of `Kestrun.Hosting.Options.MapRouteOptions` that contains the configuration for the route.
        This parameter is used to specify various options for the route, such as HTTP verbs, path, authorization schemes, and more.
    .PARAMETER Verbs
        Alias: Method
        The HTTP verbs (GET, POST, etc.) that the route should respond to. Defaults to GET.
    .PARAMETER Pattern
        Alias: Path
        The URL path for the new route.
    .PARAMETER ScriptBlock
        The script block to be executed when the route is accessed.
    .PARAMETER Code
        The code to be executed when the route is accessed, used in conjunction with the Language parameter.
    .PARAMETER Language
        The scripting language of the code to be executed.
    .PARAMETER CodeFilePath
        The file path to the code to be executed when the route is accessed.
    .PARAMETER AuthorizationSchema
        An optional array of authorization schemes that the route requires.
    .PARAMETER AuthorizationPolicy
        An optional array of authorization policies that the route requires.
    .PARAMETER ExtraImports
        An optional array of additional namespaces to import for the route.
    .PARAMETER ExtraRefs
        An optional array of additional assemblies to reference for the route.
    .PARAMETER AllowDuplicate
        If specified, allows the addition of duplicate routes with the same path and HTTP verb.
    .PARAMETER Arguments
        An optional hashtable of arguments to pass to the script block or code.
    .PARAMETER DuplicateAction
        Specifies the action to take if a duplicate route is detected. Options are 'Throw', 'Skip', 'Allow', or 'Warn'.
        Default is 'Throw', which will raise an error if a duplicate route is found.
    .PARAMETER PassThru
        If specified, the function will return the created route object.
    .OUTPUTS
        Returns the Kestrun server instance with the new route added.
    .EXAMPLE
        Add-KrMapRoute -Server $myServer -Path "/myroute" -ScriptBlock { Write-Host "Hello, World!" }
        Adds a new map route to the specified Kestrun server with the given path and script block.
    .EXAMPLE

        Add-KrMapRoute -Server $myServer -Path "/myroute" -Code "Write-Host 'Hello, World!'" -Language PowerShell
        Adds a new map route to the specified Kestrun server with the given path and code.
    .EXAMPLE
        Get-KrServer | Add-KrMapRoute -Path "/myroute" -ScriptBlock { Write-Host "Hello, World!" } -PassThru
        Adds a new map route to the current Kestrun server and returns the route object.
    .NOTES
        This function is part of the Kestrun PowerShell module and is used to manage routes
#>
function Add-KrMapRoute {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(defaultParameterSetName = 'ScriptBlock', PositionalBinding = $true)]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options', ValueFromPipeline = $true)]
        [Kestrun.Hosting.Options.MapRouteOptions]$Options,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [Alias('Method')]
        [Kestrun.Utilities.HttpVerb[]]$Verbs = @([Kestrun.Utilities.HttpVerb]::Get),

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [ValidatePattern('^/')]
        [Alias('Path')]
        [string]$Pattern = '/',

        [Parameter(Mandatory = $true, Position = 0, ParameterSetName = 'ScriptBlock')]
        [scriptblock]$ScriptBlock,

        [Parameter(Mandatory = $true, ParameterSetName = 'Code')]
        [Alias('CodeBlock')]
        [string]$Code,

        [Parameter(Mandatory = $true, ParameterSetName = 'Code')]
        [Kestrun.Scripting.ScriptLanguage]$Language,

        [Parameter(Mandatory = $true, ParameterSetName = 'CodeFilePath')]
        [string]$CodeFilePath,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [string[]]$AuthorizationSchema = $null,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [string[]]$AuthorizationPolicy = $null,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [string[]]$ExtraImports = $null,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [System.Reflection.Assembly[]]$ExtraRefs = $null,

        [Parameter(ParameterSetName = 'ScriptBlock')]
        [Parameter(ParameterSetName = 'Code')]
        [Parameter(ParameterSetName = 'CodeFilePath')]
        [hashtable]$Arguments,

        [Parameter()]
        [switch]$AllowDuplicate,

        [Parameter()]
        [ValidateSet('Throw', 'Skip', 'Allow', 'Warn')]
        [string]$DuplicateAction = 'Throw',

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        # Ensure the server instance is resolved
        $Server = Resolve-KestrunServer -Server $Server
        if ($null -eq $Server) {
            throw 'Server is not initialized. Please ensure the server is configured before setting options.'
        }
    }
    process {

        $exists = Test-KrRoute -Path $Pattern -Verb $Verbs

        if ($exists) {
            if ($AllowDuplicate -or $DuplicateAction -eq 'Allow') {
                Write-KrLog -Level Warning -Message "Route '{Path}' ({Verbs}) already exists; adding another." -Properties $Pattern, ($Verbs -join ',')
            } elseif ($DuplicateAction -eq 'Skip') {
                Write-KrLog -Level Verbose -Message "Route '{Path}' ({Verbs}) exists; skipping." -Properties $Pattern, ($Verbs -join ',')
                return
            } elseif ($DuplicateAction -eq 'Warn') {
                Write-KrLog -Level Warning -Message "Route '{Path}' ({Verbs}) already exists." -Properties $Pattern, ($Verbs -join ',')
            } else {
                throw [System.InvalidOperationException]::new(
                    "Route '$Pattern' with method(s) $($Verbs -join ',') already exists.")
            }
        }

        # -- if Options parameter is used, we can skip the rest of the parameters
        if ($PSCmdlet.ParameterSetName -ne 'Options') {
            $Options = [Kestrun.Hosting.Options.MapRouteOptions]::new()
            $Options.HttpVerbs = $Verbs
            $Options.Pattern = $Pattern
            $Options.ExtraImports = $ExtraImports
            $Options.ExtraRefs = $ExtraRefs
            if ($null -ne $AuthorizationSchema) {
                $Options.RequireSchemes = $AuthorizationSchema
            }
            if ($null -ne $AuthorizationPolicy) {
                $Options.RequirePolicies = $AuthorizationPolicy
            }

            if ($null -ne $Arguments) {
                $dict = [System.Collections.Generic.Dictionary[string, object]]::new()
                foreach ($key in $Arguments.Keys) {
                    $dict[$key] = $Arguments[$key]
                }
                $Options.Arguments = $dict
            }
            switch ($PSCmdlet.ParameterSetName) {
                'ScriptBlock' {
                    $Options.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                    $Options.Code = $ScriptBlock.ToString()
                }
                'Code' {
                    $Options.Language = $Language
                    $Options.Code = $Code
                }
                'CodeFilePath' {
                    if (-not (Test-Path -Path $CodeFilePath)) {
                        throw "The specified code file path does not exist: $CodeFilePath"
                    }
                    $extension = Split-Path -Path $CodeFilePath -Extension
                    switch ($extension) {
                        '.ps1' {
                            $Options.ValidateCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::PowerShell
                        }
                        '.cs' {
                            $Options.ValidateCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::CSharp
                        }
                        '.vb' {
                            $Options.ValidateCodeSettings.Language = [Kestrun.Scripting.ScriptLanguage]::VisualBasic
                        }
                        default {
                            throw "Unsupported '$extension' code file extension."
                        }
                    }
                    $Options.ValidateCodeSettings.Code = Get-Content -Path $CodeFilePath -Raw
                }
            }
        } else {
            Write-Verbose 'Using provided MapRouteOptions instance.'
        }
        if ($Server.RouteGroupStack.Count -gt 0) {
            $grp = $Server.RouteGroupStack.Peek()
            $Options = _KrMerge-MRO -Parent $grp -Child $Options
        }
        [Kestrun.Hosting.KestrunHostMapExtensions]::AddMapRoute($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) {
            # if the PassThru switch is specified, return the server instance
            # Return the modified server instance
            return $Server
        }
    }
}


<#
    .SYNOPSIS
        Adds localization middleware to the Kestrun server.
    .DESCRIPTION
        Enables PowerShell-style localization using string table files (Messages.psd1).
        The middleware resolves the culture once per request and exposes localized strings via
        Context.Strings and the Localizer variable in route runspaces.
    .PARAMETER Server
        The Kestrun server instance to configure.
    .PARAMETER Options
        A Kestrun.Localization.KestrunLocalizationOptions instance. Overrides individual parameters.
    .PARAMETER DefaultCulture
        Default culture used when no match is found. Default is 'en-US'.
    .PARAMETER SupportedCultures
        Optional list of supported cultures. If provided, only these cultures are used for resolution.
    .PARAMETER ResourcesBasePath
        Base path for localization resources. Default is 'i18n'.
    .PARAMETER FileName
        Localization file name. Default is 'Messages.psd1'.
    .PARAMETER QueryKey
        Query string key used to request a culture. Default is 'lang'.
    .PARAMETER CookieName
        Cookie name used to request a culture. Default is 'lang'.
    .PARAMETER EnableAcceptLanguage
        Enables Accept-Language header resolution when specified.
    .PARAMETER EnableQuery
        Enables query string resolution when specified.
    .PARAMETER EnableCookie
        Enables cookie resolution when specified.
    .PARAMETER PassThru
        Returns the server instance for chaining.
    .EXAMPLE
        Add-KrLocalizationMiddleware -ResourcesBasePath './Assets/i18n' -SupportedCultures @('en-US','it-IT')
    .EXAMPLE
        $opts = [Kestrun.Localization.KestrunLocalizationOptions]::new()
        $opts.DefaultCulture = 'en-US'
        $opts.ResourcesBasePath = 'i18n'
        Add-KrLocalizationMiddleware -Options $opts -PassThru
#>
function Add-KrLocalizationMiddleware {
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding(DefaultParameterSetName = 'Items')]
    [OutputType([Kestrun.Hosting.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.Hosting.KestrunHost]$Server,

        [Parameter(Mandatory = $true, ParameterSetName = 'Options')]
        [Kestrun.Localization.KestrunLocalizationOptions]$Options,

        [Parameter(ParameterSetName = 'Items')]
        [ValidatePattern('^[a-zA-Z]{2}(-[a-zA-Z]{2})?$')]
        [string]$DefaultCulture = 'en-US',

        [Parameter(ParameterSetName = 'Items')]
        [ValidatePattern('^[a-zA-Z]{2}(-[a-zA-Z]{2})?$')]
        [string[]]$SupportedCultures,

        [Parameter(ParameterSetName = 'Items')]
        [string]$ResourcesBasePath = 'i18n',

        [Parameter(ParameterSetName = 'Items')]
        [string]$FileName = 'Messages.psd1',

        [Parameter(ParameterSetName = 'Items')]
        [string]$QueryKey = 'lang',

        [Parameter(ParameterSetName = 'Items')]
        [string]$CookieName = 'lang',

        [Parameter(ParameterSetName = 'Items')]
        [switch]$DisableAcceptLanguage,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$EnableQuery,

        [Parameter(ParameterSetName = 'Items')]
        [switch]$EnableCookie,

        [Parameter()]
        [switch]$PassThru
    )
    begin {
        $Server = Resolve-KestrunServer -Server $Server
    }
    process {
        if ($PSCmdlet.ParameterSetName -eq 'Items') {
            $Options = [Kestrun.Localization.KestrunLocalizationOptions]::new()
            $Options.DefaultCulture = $DefaultCulture
            $Options.ResourcesBasePath = (Resolve-KrPath -Path $ResourcesBasePath -KestrunRoot)
            $Options.FileName = $FileName
            $Options.QueryKey = $QueryKey
            $Options.CookieName = $CookieName

            if ($PSBoundParameters.ContainsKey('SupportedCultures')) { $Options.SupportedCultures = $SupportedCultures }
            if ($PSBoundParameters.ContainsKey('DisableAcceptLanguage')) { $Options.EnableAcceptLanguage = -not $DisableAcceptLanguage.IsPresent }
            if ($PSBoundParameters.ContainsKey('EnableQuery')) { $Options.EnableQuery = $EnableQuery.IsPresent }
            if ($PSBoundParameters.ContainsKey('EnableCookie')) { $Options.EnableCookie = $EnableCookie.IsPresent }
        }

        [Kestrun.Hosting.KestrunLocalizationExtensions]::AddLocalization($Server, $Options) | Out-Null

        if ($PassThru.IsPresent) { return $Server }
    }
}

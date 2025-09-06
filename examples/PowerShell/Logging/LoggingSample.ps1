try {
    # Get the path of the current script
    # This allows the script to be run from any location
    $ScriptPath = (Split-Path -Parent -Path $MyInvocation.MyCommand.Path)
    # Determine the script path and Kestrun module path
    $powerShellExamplesPath = (Split-Path -Parent ($ScriptPath))
    # Determine the script path and Kestrun module path
    $examplesPath = (Split-Path -Parent ($powerShellExamplesPath))
    # Get the parent directory of the examples path
    # This is useful for locating the Kestrun module
    $kestrunPath = Split-Path -Parent -Path $examplesPath
    # Construct the path to the Kestrun module
    # This assumes the Kestrun module is located in the src/PowerShell/Kestr
    $kestrunModulePath = "$kestrunPath/src/PowerShell/Kestrun/Kestrun.psm1"

    # Import the Kestrun module from the source path if it exists, otherwise from installed modules
    if (Test-Path -Path $kestrunModulePath -PathType Leaf) {
        Import-Module $kestrunModulePath -Force -ErrorAction Stop
    } else {
        Import-Module -Name 'Kestrun' -MaximumVersion 2.99 -ErrorAction Stop
    }
} catch {
    Write-Error "Failed to import Kestrun module: $_"
    Write-Error "Ensure the Kestrun module is installed or the path is correct."
    exit 1
}


New-KrServer -Name "MyKestrunServer"

# Level switch allows you to switch minimum logging level
$levelSwitch = New-KrLevelSwitch -MinimumLevel Verbose

$l0 = New-KrLogger |
    Set-KrMinimumLevel -Value Verbose -ToPreference |
    Add-KrEnrichEnvironment |
    Add-EnrichWithExceptionDetail |
    Add-KrEnrichFromLogContext |
    Add-KrSinkPowerShell |
    Add-KrSinkConsole -OutputTemplate "[{MachineName} {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}" |
    Register-KrLogger -PassThru -SetAsDefault -Name "DefaultLogger"

Write-KrLog -Level Information -Message 'Some default log'

Close-KrLogger -Logger $l0

# Setup new logger
New-KrLogger |
    Set-KrMinimumLevel -Value Verbose |
    Add-KrEnrichEnvironment |
    Add-EnrichWithExceptionDetail |
    Add-KrSinkFile -Path ".\logs\test-.log" -RollingInterval Hour -OutputTemplate '{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception} {Properties:j}{NewLine}' |
    Add-KrSinkConsole -OutputTemplate "[{MachineName} {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}" |
    Register-KrLogger -Name "Logger1"

Register-KrLogger -FilePath ".\logs\test2-.log" -Console -MinimumLevel Verbose -Name "Logger2"

# Write-KrLog -Level Verbose "test verbose"
Write-KrLog -Level Debug -Message "test debug asd" -Name "Logger1"
Set-KrDefaultLogger -Name "Logger2"
Write-KrLog -Level Information -Message $null
Write-KrLog -Level Information -Message ''
Write-KrLog -Level Information -Message 'asd {0} - {1}' -Properties $null, '' -Exception $null

Write-KrLog -Level Debug -Message "test debug asdasdsad"

Write-KrLog -Level Warning "test warning" -Name "Logger1"

Set-KrDefaultLogger -Name "Logger1"

Write-KrLog -Level Information "test info"

Write-KrLog -Level Error -Message "test error {asd}, {num}, {@levelSwitch}" -Properties "test1", 123, $levelSwitch -Name "Logger2"

try {
    Get-Content -Path 'asd' -ErrorAction Stop
} catch {
    Write-KrLog -Level Fatal -ErrorRecord $_ -Message 'Error while reading file!'
}


New-KrLogger |
    Set-KrMinimumLevel -Value Verbose |
    Add-KrEnrichEnvironment |
    Add-EnrichWithExceptionDetail |
    Add-KrSinkConsole -OutputTemplate "[{MachineName} {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}" |
    Add-KrSinkEventLog -Source "MyApp" -ManageEventSource |
    Register-KrLogger -Name "Logger3"
Write-KrLog -Level Information -Name "Logger3" -Message "test info"

Close-KrLogger

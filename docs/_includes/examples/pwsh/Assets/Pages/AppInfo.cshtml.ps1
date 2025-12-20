[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()
# These variables EXIST because they were defined
# before Enable-KrConfiguration in the main script

$Model = [pscustomobject]@{
    App = $AppInfo
    Flags = $FeatureFlags
    Motd = $Motd
    Host = (Expand-KrObject -InputObject $KrServer -PassThru)
}

# --- Extra runtime/environment info ---

# Best-effort access to IServiceProvider (depends on how KrServer is shaped)

try {
    $webEnv = $Context.RequestServices.GetService([Microsoft.AspNetCore.Hosting.IWebHostEnvironment])
} catch {
    $webEnv = $null
}
$proc = [System.Diagnostics.Process]::GetCurrentProcess()

$a = @{}

$environment = if ($null -eq $webEnv) {
    @{
        EnvironmentName = 'Unknown'
        ApplicationName = 'Unknown'
        ContentRootPath = 'Unknown'
        WebRootPath = 'Unknown'
        WebRootFileProvider = 'Unknown'
    }
} else {
    @{
        EnvironmentName = $webEnv.EnvironmentName
        ApplicationName = $webEnv.ApplicationName
        ContentRootPath = $webEnv.ContentRootPath
        WebRootPath = $webEnv.WebRootPath
        WebRootFileProvider = $webEnv.WebRootFileProvider.GetType().FullName
    }
}
$environment.MachineName = [System.Environment]::MachineName
$environment.UserName = try { [System.Environment]::UserName } catch { 'Unknown' }

Expand-KrObject -InputObject $environment
$Model | Add-Member -Force -NotePropertyName Environment -NotePropertyValue ([pscustomobject]$environment)

$Model | Add-Member -Force -NotePropertyName DotNet -NotePropertyValue ([pscustomobject]@{
        Runtime = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
        ProcessArch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
        OSArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    })

$Model | Add-Member -Force -NotePropertyName OS -NotePropertyValue ([pscustomobject]@{
        Description = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        IsContainer = ([System.Environment]::GetEnvironmentVariable('DOTNET_RUNNING_IN_CONTAINER') -eq 'true')
    })

$Model | Add-Member -Force -NotePropertyName Process -NotePropertyValue ([pscustomobject]@{
        PID = $proc.Id
        StartTime = try { $proc.StartTime.ToUniversalTime().ToString('o') } catch { $null } # ISO 8601 UTC
    })
    
$kestrelAsm = [Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServer].Assembly
$kestrelName = $kestrelAsm.GetName()

$Model | Add-Member -Force -NotePropertyName Kestrel -NotePropertyValue ([pscustomobject]@{
        Version = $kestrelName.Version.ToString()
        Location = $kestrelAsm.Location
    })

$psVersion = $PSVersionTable

$Model | Add-Member -Force -NotePropertyName PowerShell -NotePropertyValue ([pscustomobject]@{
        Edition = $psVersion.PSEdition
        Version = $psVersion.PSVersion.ToString()
        Compatible = ($psVersion.PSCompatibleVersions -join ', ')
        RemotingProto = $psVersion.PSRemotingProtocolVersion?.ToString()
        Serialization = $psVersion.SerializationVersion?.ToString()
        OS = $psVersion.OS
        Platform = $psVersion.Platform
    })


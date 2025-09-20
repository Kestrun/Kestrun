param()
# Runtime feature / version tests for Kestrun PowerShell surface
# Mirrors C# unit tests for KestrunRuntimeInfo

BeforeAll {
    $path = $PSCommandPath
    $kestrunRoot = (Split-Path -Parent ((Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $path))))))
    $kestrunPath = Join-Path -Path $kestrunRoot -ChildPath 'src' -AdditionalChildPath 'PowerShell', 'Kestrun'
    $examplePath = Join-Path -Path $kestrunRoot -ChildPath 'docs' -AdditionalChildPath 'pwsh', 'tutorial', 'examples'
    $exampleScript = Get-Content -Path $examplePath/1.1-Hello-World.ps1 -Raw
    $exampleScript = $exampleScript -replace 'Start-KrServer', @"
Add-KrMapRoute -Verbs Get -Pattern "/shutdown" -ScriptBlock {
    Stop-KrServer
}

Start-KrServer
"@

    $headerScript = @"
# Ensure module is imported (_.Tests.ps1 normally does this too, but be defensive)
if (-not (Get-Module -Name Kestrun)) {
    if (Test-Path -Path "$kestrunPath/Kestrun.psm1" -PathType Leaf) {
        Import-Module "$kestrunPath/Kestrun.psm1" -Force -ErrorAction Stop
    } else {
        throw "Kestrun module not found at $kestrunPath"
    }
}

"@

    $script:tmpScriptPath = Join-Path -Path $($env:TEMP) -ChildPath "test.ps1"

    Out-File -FilePath $script:tmpScriptPath -InputObject ($headerScript + $exampleScript) -Encoding UTF8 -Force

    $script:Process = Start-Process -FilePath 'pwsh' -ArgumentList "-NoLogo", "-NoProfile", "-File `"$($script:tmpScriptPath)`"" -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 5
}

AfterAll {
    if ($null -ne $script:Process) {
        try {
            Invoke-WebRequest -Uri 'http://localhost:5000/shutdown' -Method Get -UseBasicParsing | Out-Null
        } catch {
            # Ignore errors, server might already be down
            Write-Debug "Error sending shutdown request: $_"
        }
        if (-not $script:Process.HasExited) {
            $script:Process | Stop-Process -Force
        }
        $script:Process.Dispose()
    }
    Remove-Item -Path $script:tmpScriptPath -Force -ErrorAction SilentlyContinue
}

Describe 'Runtime Version Information' {
    It 'Hello World route returns expected response' {
        $response = Invoke-WebRequest -Uri 'http://localhost:5000/hello' -Method Get
        $response.Content | Should -Be "Hello, World!"
        $response.StatusCode | Should -Be 200
    }
}

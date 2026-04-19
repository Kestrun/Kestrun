param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Enable-KrConfiguration function capture' -Tag 'Integration' {

    BeforeAll {
        $script:instance = Start-ExampleScript -ScriptBlock {
            param(
                [int]$Port = 5000,
                [IPAddress]$IPAddress = [IPAddress]::Loopback
            )

            $helperRoot = Join-Path $PSScriptRoot ('split-helper-' + [System.IO.Path]::GetFileNameWithoutExtension($PSCommandPath))
            $null = New-Item -ItemType Directory -Path $helperRoot -Force

            $helperPath = Join-Path $helperRoot 'SplitHelpers.ps1'
            Set-Content -LiteralPath $helperPath -Encoding utf8NoBOM -Value @'
function Get-SplitGreeting {
    return "split-ok"
}
'@

            . $helperPath

            New-KrServer -Name 'SplitFunctionCaptureServer'
            Add-KrEndpoint -Port $Port -IPAddress $IPAddress
            Add-KrMapRoute -Verbs Get -Pattern '/split' -ScriptBlock {
                Write-KrJsonResponse @{ value = (Get-SplitGreeting) } -StatusCode 200
            }

            Enable-KrConfiguration
            Start-KrServer
        }
    }

    AfterAll {
        if ($script:instance) {
            $helperRoot = Join-Path (Split-Path -Parent $script:instance.TempPath) ('split-helper-' + [System.IO.Path]::GetFileNameWithoutExtension($script:instance.TempPath))
            if (Test-Path -LiteralPath $helperRoot) {
                Remove-Item -LiteralPath $helperRoot -Recurse -Force
            }

            Stop-ExampleScript -Instance $script:instance
            Write-KrExampleInstanceOnFailure -Instance $script:instance
        }
    }

    It 'includes helper functions from dot-sourced child files in route runspaces' {
        $response = Invoke-RestMethod -Uri "$($script:instance.Url)/split" -Method Get
        $response.value | Should -Be 'split-ok'
    }
}

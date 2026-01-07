param()

BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Enable-KrConfiguration variable capture' -Tag 'Integration' {

    Context 'Typed variables vs attributed variables' {

        BeforeAll {
            $script:instance = Start-ExampleScript -ScriptBlock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )

                # Variables to be captured from the caller script by Enable-KrConfiguration.
                [int]$SharedValue = 5000
                [ValidateNotNull()][int]$AttributedValue = 123

                New-KrServer -Name 'VarCaptureServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                Add-KrMapRoute -Verbs Get -Pattern '/vars' -ScriptBlock {
                    Write-KrJsonResponse @{
                        sharedValue = $SharedValue
                        attributedValue = $AttributedValue
                    }
                }

                Enable-KrConfiguration
                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) { Stop-ExampleScript -Instance $script:instance }
        }

        It 'includes typed variables without attributes' {
            $response = Invoke-RestMethod -Uri "$($script:instance.Url)/vars" -Method Get
            $response.sharedValue | Should -Be 5000
        }

        It 'excludes variables decorated with attributes (WithoutAttributesOnly)' {
            $response = Invoke-RestMethod -Uri "$($script:instance.Url)/vars" -Method Get
            $response.attributedValue | Should -BeNullOrEmpty
        }
    }
}

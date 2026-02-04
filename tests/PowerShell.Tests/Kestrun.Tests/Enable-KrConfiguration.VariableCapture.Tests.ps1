param()
BeforeAll {
    . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')
}

Describe 'Enable-KrConfiguration variable capture' -Tag 'Integration' {

    Context 'Typed variables vs attributed variables' {

        BeforeAll {
            $script:instance = Start-ExampleScript -Scriptblock {
                param(
                    [int]$Port = 5000,
                    [IPAddress]$IPAddress = [IPAddress]::Loopback
                )

                # Variables to be captured from the caller script by Enable-KrConfiguration.
                $simpleValue = 'A simple string value'
                [int]$SharedValue = 5000
                [ValidateNotNull()][int]$AttributedValue = 123

                New-KrServer -Name 'VarCaptureServer'
                Add-KrEndpoint -Port $Port -IPAddress $IPAddress

                Enable-KrConfiguration

                Add-KrMapRoute -Verbs Get -Pattern '/vars' -Scriptblock {
                    Write-KrJsonResponse @{
                        sharedValue = $SharedValue
                        attributedValue = $AttributedValue
                        simpleValue = $simpleValue
                    }
                }


                Start-KrServer
            }
        }

        AfterAll {
            if ($script:instance) {
                # Stop the example script
                Stop-ExampleScript -Instance $script:instance
                # Diagnostic info on failure
                Write-KrExampleInstanceOnFailure -Instance $script:instance
            }
        }

        It 'includes typed variables without attributes' {
            $response = Invoke-RestMethod -Uri "$($script:instance.Url)/vars" -Method Get
            $response.sharedValue | Should -Be 5000
        }

        It 'excludes variables decorated with attributes (WithoutAttributesOnly)' {
            $response = Invoke-RestMethod -Uri "$($script:instance.Url)/vars" -Method Get
            $response.attributedValue | Should -BeNullOrEmpty
        }

        It 'includes simple untyped variables' {
            $response = Invoke-RestMethod -Uri "$($script:instance.Url)/vars" -Method Get
            $response.simpleValue | Should -Be 'A simple string value'
        }
    }
}

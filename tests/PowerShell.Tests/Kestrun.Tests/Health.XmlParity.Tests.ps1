<#
Validates numeric probe data representation in XML health response.
* Ensures <intVal>42</intVal> and <floatVal>12.5</floatVal> appear and when parsed values can be cast to numeric.
* Mirrors JSON & YAML parity tests.
#>

Describe 'Validates numeric probe data representation in XML health response' -Tag 'Health' {
    BeforeAll { . (Join-Path $PSScriptRoot '.\PesterHelpers.ps1')

        $scriptBlock = {
            param(
                [int]$Port = 5000,
                [IPAddress]$IPAddress = [IPAddress]::Loopback
            )
            New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault
            New-KrServer -Name 'NumericTest'

            # Add a listener on the configured port and IP address
            Add-KrEndpoint -Port $Port -IPAddress $IPAddress

            Add-KrPowerShellRuntime

            # Add a simple health probe that returns numeric data
            Add-KrHealthProbe -Name 'NumProbe' -Scriptblock {
                New-KrProbeResult Healthy 'OK' -Data @{ intVal = 42; floatVal = 12.5 }
            }

            # Add a health endpoint that returns XML
            Add-KrHealthEndpoint -Pattern '/healthz' -ResponseContentType Xml

            Enable-KrConfiguration

            # Start the server asynchronously
            Start-KrServer -CloseLogsOnExit
        }
        $script:instance = Start-ExampleScript -Scriptblock $scriptBlock
    }
    AfterAll { if ($script:instance) { Stop-ExampleScript -Instance $script:instance } }


    It 'GET /healthz returns numeric probe data as numbers in JSON' {
        $resp = Invoke-WebRequest -Uri "$($script:instance.Url)/healthz" -Headers @{ Accept = 'application/xml' }
        $resp | Should -Not -BeNullOrEmpty
        $resp.Content | Should -Not -BeNullOrEmpty
        $xml = [xml]$resp.Content
        $xml | Should -Not -BeNullOrEmpty
        $probeNodes = $xml.SelectNodes("//*[local-name()='NumProbe']")
        if (-not $probeNodes -or $probeNodes.Count -eq 0) {
            # Fallback: health report structure <Response><Probes><Item>...</Item></Probes> etc.
            $probeNodes = $xml.SelectNodes("//*[local-name()='Probes']/*[local-name()='Item'][*[local-name()='Name']='NumProbe']")
        }

        (-not $probeNodes -or $probeNodes.Count -eq 0) | Should -BeFalse

        # Attempt to find data node children (Data is dictionary -> elements <intVal>42</intVal>, <floatVal>12.5</floatVal>)
        $intNode = $xml.SelectSingleNode("//*[local-name()='intVal']")
        $floatNode = $xml.SelectSingleNode("//*[local-name()='floatVal']")
        (-not $intNode -or -not $floatNode) | Should -BeFalse

        $intVal = $intNode.InnerText
        $floatVal = $floatNode.InnerText

        (  ([int]::TryParse($intVal, [ref]([int]0)))) | Should -BeTrue
        (  ([double]::TryParse($floatVal, [ref]([double]0)))) | Should -BeTrue

    }
}

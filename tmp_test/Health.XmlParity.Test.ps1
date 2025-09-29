<#
Validates numeric probe data representation in XML health response.
* Ensures <intVal>42</intVal> and <floatVal>12.5</floatVal> appear and when parsed values can be cast to numeric.
* Mirrors JSON & YAML parity tests.
#>
Import-Module "$PSScriptRoot/../../src/PowerShell/Kestrun/Kestrun.psd1" -Force

New-KrServer -Name 'XmlParity'
Add-KrEndpoint -Port 5017 -IPAddress ([IPAddress]::Loopback)
Add-KrPowerShellRuntime

Add-KrHealthProbe -Name 'NumProbe' -ScriptBlock {
    New-KrProbeResult Healthy 'OK' -Data @{ intVal = 42; floatVal = 12.5 }
}

Enable-KrConfiguration
Add-KrHealthEndpoint -Pattern '/healthz' -ResponseContentType Xml
Start-KrServer

try {
    Start-Sleep -Milliseconds 250
    $resp = Invoke-WebRequest -Uri 'http://localhost:5017/healthz' -Headers @{ Accept = 'application/xml' }
    if (-not $resp -or -not $resp.Content) { throw 'No XML response content received.' }
    $xml = [xml]$resp.Content

    # Navigate: root element name depends on default (Response) with child representing HealthReport (root is <Response> with nested?)
    # Simpler: search for NumProbe node.
    $probeNodes = $xml.SelectNodes("//*[local-name()='NumProbe']")
    if (-not $probeNodes -or $probeNodes.Count -eq 0) {
        # Fallback: health report structure <Response><Probes><Item>...</Item></Probes> etc.
        $probeNodes = $xml.SelectNodes("//*[local-name()='Probes']/*[local-name()='Item'][*[local-name()='Name']='NumProbe']")
    }
    if (-not $probeNodes -or $probeNodes.Count -eq 0) { throw 'NumProbe not located in XML structure.' }
    $probeNode = $probeNodes[0]

    # Attempt to find data node children (Data is dictionary -> elements <intVal>42</intVal>, <floatVal>12.5</floatVal>)
    $intNode = $xml.SelectSingleNode("//*[local-name()='intVal']")
    $floatNode = $xml.SelectSingleNode("//*[local-name()='floatVal']")
    if (-not $intNode -or -not $floatNode) { throw 'Numeric data elements not found (intVal / floatVal).' }

    $intVal = $intNode.InnerText
    $floatVal = $floatNode.InnerText

    if (-not ([int]::TryParse($intVal, [ref]([int]0)))) { throw "intVal not integer parseable: $intVal" }
    if (-not ([double]::TryParse($floatVal, [ref]([double]0)))) { throw "floatVal not floating parseable: $floatVal" }
} finally {
    Stop-KrServer -Name 'XmlParity' -Force -ErrorAction SilentlyContinue | Out-Null
}

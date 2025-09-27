
<#
    Sample: Structured Formats (XML / YAML / CSV)
    Purpose: Demonstrate XML, YAML, and CSV responses in a Kestrun server.
    File:    9.2-Structured-Xml-Yaml-Csv.ps1
    Notes:   Shows alternative serialization formats for objects.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.2'

# 3. Listener
Add-KrListener -IPAddress '127.0.0.1' -Port 5000

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration
Enable-KrConfiguration

# XML route
Add-KrMapRoute -Pattern '/xml' -Verbs GET -ScriptBlock {
    Write-KrXmlResponse -InputObject @{ Id = 1; Name = 'Alpha'; Nested = @{ X = 10; Y = 20 } }
}

# YAML route
Add-KrMapRoute -Pattern '/yaml' -Verbs GET -ScriptBlock {
    @{ env = 'dev'; enabled = $true; tags = @('one', 'two') },
    @{ Id = 1; Name = 'Alpha'; Nested = @{ X = 10; Y = 20 } } | Write-KrYamlResponse -ContentType 'application/x-yaml'
}

# CSV route
Add-KrMapRoute -Pattern '/csv' -Verbs GET -ScriptBlock {
    $data = 1..5 | ForEach-Object { @{ Id = $_; Square = ($_ * $_) } }
    Write-KrCsvResponse -InputObject $data
}

# Start the server
Start-KrServer -CloseLogsOnExit

<#
 Process Probe Example
 Demonstrates using a process probe to execute a lightweight external command.
#>
## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Health Process Probe'

## 3. Listener (port 5000)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

## 4. Runtime
Add-KrPowerShellRuntime

## 5. Enable configuration
Enable-KrConfiguration

## 6. Process probe - executes 'dotnet --info' (with timeout)
Add-KrHealthProcessProbe -Name 'DotNetInfo' -FilePath 'dotnet' -Arguments '--info' -Timeout '00:00:03' -Tags 'system', 'self'

## 7. Health endpoint
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'self', 'system' -ProbeTimeout '00:00:05'

## 8. Start server
Start-KrServer

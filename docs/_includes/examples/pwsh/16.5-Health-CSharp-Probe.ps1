<#
 C# Inline Probe Example
 Demonstrates an inline C# probe computing a random success/failure.
#>
## 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

## 2. Server
New-KrServer -Name 'Health CSharp Probe'

## 3. Listener (port 5000)
Add-KrListener -Port 5000 -IPAddress ([IPAddress]::Loopback)

## 4. Runtime
Add-KrPowerShellRuntime

## 5. Enable configuration
Enable-KrConfiguration

$csharp = @"
using System;
using System.Collections.Generic;
using Kestrun.Health;
public static class ProbeImpl {
    public static ProbeResult Run() {
        var rnd = new Random();
        int value = rnd.Next(0,100);
        var status = value < 70 ? ProbeStatus.Healthy : (value < 90 ? ProbeStatus.Degraded : ProbeStatus.Unhealthy);
        return new ProbeResult(status, $"Random value={value}", new Dictionary<string,object?> { {"value", value } });
    }
}
ProbeImpl.Run();
"@

## 6. C# inline code compiled/executed at runtime
Add-KrHealthProbe -Name 'RandomCSharp' -Tags 'self', 'random' -Code $csharp -Language CSharp

## 7. Health endpoint
Add-KrHealthEndpoint -Pattern '/healthz' -DefaultTags 'self', 'random' -ProbeTimeout '00:00:05'

## 8. Start server
Start-KrServer

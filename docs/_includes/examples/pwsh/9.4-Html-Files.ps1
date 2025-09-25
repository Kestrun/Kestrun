
<#
    Sample: HTML Templates & File Downloads
    Purpose: Demonstrate HTML templating and file download in a Kestrun server.
    File:    9.4-Html-Files.ps1
    Notes:   Shows HTML file, inline template, and file download routes.
#>

# 1. Logging
New-KrLogger | Add-KrSinkConsole | Register-KrLogger -Name 'console' -SetAsDefault

# 2. Server
New-KrServer -Name 'Responses 9.4'

# 3. Listener
Add-KrEndpoint -IPAddress '127.0.0.1' -Port 5000

# 4. Runtime
Add-KrPowerShellRuntime

# Finalize configuration and start server
Enable-KrConfiguration

# HTML file route
Add-KrMapRoute -Pattern '/page' -Verbs GET -ScriptBlock {
    Write-KrHtmlResponse -FilePath 'Assets/wwwroot/about.html' -StatusCode 200 -Variables @{ Title = 'About'; Body = 'Injected body variable' }
}

# Inline template route
Add-KrMapRoute -Pattern '/inline' -Verbs GET -ScriptBlock {
    $tpl = '<html><body><h1>{{Title}}</h1><p>{{Body}}</p></body></html>'
    Write-KrHtmlResponse -Template $tpl -Variables @{ Title = 'Inline'; Body = 'Rendered via template var expansion' }
}

# File download route
Add-KrMapRoute -Pattern '/download' -Verbs GET -ScriptBlock {
    Write-KrFileResponse -FilePath 'Assets/wwwroot/files/sample.txt' -FileDownloadName 'report.txt' -ContentDisposition Attachment
}

# Start the server
Start-KrServer -CloseLogsOnExit

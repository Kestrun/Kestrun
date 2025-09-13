# 9.4 HTML Templates & File Downloads

New-KrServer -Name 'Responses 9.4' | Out-Null
Add-KrListener -Url 'http://127.0.0.1:5094' | Out-Null
Add-KrRuntimePowerShell | Out-Null

# HTML file (Assets/wwwroot/about.html for demo if present)
Add-KrMapRoute -Path '/page' -Method GET -ScriptBlock {
    Write-KrHtmlResponse -FilePath 'Assets/wwwroot/about.html' -StatusCode 200 -Variables @{ Title = 'About'; Body = 'Injected body variable'; }
}

# Inline template
Add-KrMapRoute -Path '/inline' -Method GET -ScriptBlock {
    $tpl = '<html><body><h1>{{Title}}</h1><p>{{Body}}</p></body></html>'
    Write-KrHtmlResponse -Template $tpl -Variables @{ Title = 'Inline'; Body = 'Rendered via template var expansion'; }
}

# File download
Add-KrMapRoute -Path '/download' -Method GET -ScriptBlock {
    Write-KrFileResponse -FilePath 'Assets/wwwroot/files/sample.txt' -FileDownloadName 'report.txt' -ContentDisposition Attachment
}

Enable-KrConfiguration
Start-KrServer | Out-Null
Write-Host '9.4 server running on http://127.0.0.1:5094'

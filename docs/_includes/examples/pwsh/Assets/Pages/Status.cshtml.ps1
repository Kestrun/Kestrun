[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

$req = $Context.Request
$ip = $Context.Connection.RemoteIpAddress

# Show a small, readable subset
$keys = @('User-Agent', 'Accept', 'Accept-Language', 'Host')
$lines = foreach ($k in $keys) {
    $v = $req.Headers[$k]
    if ($v.Count -gt 0) { "$($k): $($v -join ', ')" }
}
$headersText = ($lines -join "`n")

$Model = [pscustomobject]@{
    NowUtc = [DateTime]::UtcNow.ToString('u')
    Method = $req.Method
    Path = $req.Path.Value
    RemoteIp = if ($ip) { $ip.ToString() } else { '' }
    Headers = $headersText
}

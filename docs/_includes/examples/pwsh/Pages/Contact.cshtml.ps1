$req = $Context.Request

$name = ""
$msg  = ""
$err  = ""
$submitted = $false

if ($req.Method -eq "POST") {
    try {
        # Ensure the form is read (ASP.NET Core will parse it)
        $form = $req.Form
        $name = [string]$form["name"]
        $msg  = [string]$form["message"]

        if ([string]::IsNullOrWhiteSpace($name)) { $err = "Name is required." }
        elseif ([string]::IsNullOrWhiteSpace($msg)) { $err = "Message is required." }
        else { $submitted = $true }
    }
    catch {
        $err = "Failed to read form: $($_.Exception.Message)"
    }
}

$Model = [pscustomobject]@{
    Submitted = $submitted
    Name      = $name
    Message   = $msg
    Error     = $err
}

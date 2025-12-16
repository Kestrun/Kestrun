# Pages/PSForm.cshtml.ps1

$req = $Context.Request

# Parse form only for POST
if ($req.Method -eq 'POST') {
    $form = $req.Form
    $Model = [pscustomobject]@{
        Name      = $form["name"]
        Email     = $form["email"]
        Submitted = $true
    }
}
else {
    $Model = [pscustomobject]@{
        Submitted = $false
    }
}

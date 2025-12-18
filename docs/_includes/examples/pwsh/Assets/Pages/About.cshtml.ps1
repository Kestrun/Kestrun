$Model = [pscustomobject]@{
    Project  = "Kestrun"
    Blurb    = "This sample uses Razor Pages for templating and PowerShell scripts as per-page model providers."
    Features = @(
        "Sibling script convention: Page.cshtml + Page.cshtml.ps1",
        "Per-request model via '$Model'",
        "Razor reads it as Model.Data",
        "Works under runtime compilation (C# 8 syntax in .cshtml)"
    )
}

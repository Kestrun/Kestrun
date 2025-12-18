[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', '')]
param()

$Model = [pscustomobject]@{
    Welcome = "Welcome to Kestrun ✨"
    Tagline = "A tiny site rendered by Razor, powered by PowerShell models."
    Tips    = @(
        "Open /Status to see request + server info.",
        "Open /Quotes for a dynamic quote.",
        "Open /Contact to test GET + POST.",
        "Open /Cancel to test request abort cancellation."
    )
}

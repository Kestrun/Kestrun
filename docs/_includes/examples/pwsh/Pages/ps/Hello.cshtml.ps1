<#  This is executed for the same HTTP request before Razor renders  #>

# Build any model you like – here a PSCustomObject
<#  This is executed for the same HTTP request before Razor renders  #>

# Sentences to choose from
$sentences = @(
    "The quick brown fox jumps over the lazy dog.",
    "PowerShell makes automation easy.",
    "Kestrun brings C# and PowerShell together.",
    "Hello, world! Welcome to Razor Pages.",
    "Stay curious and keep learning."
)

# Pick a random sentence
$SentenceOfTheDay = Get-Random -InputObject $sentences

# Build the model
$Model = [pscustomobject]@{
    Title    = "PowerShell-backed Razor Page"
    UserName = "Alice"
    SentenceOfTheDay = $SentenceOfTheDay
}
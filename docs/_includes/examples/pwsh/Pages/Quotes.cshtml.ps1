$quotes = @(
    @{ Quote="Simplicity is prerequisite for reliability."; Author="Edsger W. Dijkstra" },
    @{ Quote="Make it work, make it right, make it fast."; Author="Kent Beck" },
    @{ Quote="Programs must be written for people to read."; Author="Harold Abelson" },
    @{ Quote="Premature optimization is the root of all evil."; Author="Donald Knuth" }
)

$seed = [Guid]::NewGuid().ToString("N").Substring(0,8)
$rand = New-Object System.Random
$pick = $quotes[$rand.Next(0, $quotes.Count)]

$Model = [pscustomobject]@{
    Quote  = $pick.Quote
    Author = $pick.Author
    Seed   = $seed
}


using Microsoft.AspNetCore.Mvc.RazorPages;
#pragma warning disable IDE0130
namespace RazorSample.Pages;
#pragma warning restore IDE0130
public class HelloModel : PageModel
{
    public string SentenceOfTheDay { get; set; } = "No sentence";
    public string UserName { get; set; } = "Alice";
    public string Title { get; set; } = "PowerShell-backed Razor Page";

    public void OnGet()
    {
        var sentences = new List<string>
        {
            "The quick brown fox jumps over the lazy dog.",
            "PowerShell makes automation easy.",
            "Kestrun brings C# and PowerShell together.",
            "Hello, world! Welcome to Razor Pages.",
            "Stay curious and keep learning."
        };

        var random = new Random();
        SentenceOfTheDay = sentences[random.Next(sentences.Count)];
    }
}

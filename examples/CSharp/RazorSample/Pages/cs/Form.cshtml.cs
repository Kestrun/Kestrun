using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
#pragma warning disable IDE0130
namespace RazorSample.Pages;
#pragma warning restore IDE0130
public class CSharpFormModel : PageModel
{
    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    public bool Submitted { get; private set; }

    public void OnGet()
    {
        // Show form
    }

    public void OnPost() => Submitted = true;// Name and Email are auto-bound
}

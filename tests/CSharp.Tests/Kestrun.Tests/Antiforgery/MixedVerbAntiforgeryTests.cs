using System.Reflection;
using Microsoft.AspNetCore.Http;
using Kestrun.Hosting.Options;
using Kestrun.Utilities;
using Kestrun.Scripting;
using Kestrun.Hosting;
using Xunit;

namespace KestrunTests.Antiforgery;

/// <summary>
/// Focused unit tests validating the CSRF decision logic for mixed-verb routes.
/// These use reflection to invoke the private ShouldValidateCsrf method so we can
/// assert correct behavior without needing a full integration pipeline.
/// </summary>
public class MixedVerbAntiforgeryTests
{
    private static readonly MethodInfo ShouldValidateCsrfMethod = typeof(KestrunHostMapExtensions)
        .GetMethod("ShouldValidateCsrf", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not locate ShouldValidateCsrf via reflection");

    private static bool InvokeShouldValidate(MapRouteOptions opts, string method)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        var result = (bool)ShouldValidateCsrfMethod.Invoke(null, [opts, ctx])!;
        return result;
    }

    private static MapRouteOptions MixedFormRoute(bool disable = false) => new()
    {
        Pattern = "/form",
        HttpVerbs = [HttpVerb.Get, HttpVerb.Post],
        ScriptCode = new LanguageOptions
        {
            Code = "Write-Host 'noop'", // minimal placeholder
            Language = ScriptLanguage.PowerShell
        },
        DisableAntiforgery = disable
    };

    [Fact]
    public void Get_DoesNotTriggerValidation()
    {
        var route = MixedFormRoute();
        Assert.False(InvokeShouldValidate(route, HttpMethods.Get));
        Assert.False(InvokeShouldValidate(route, HttpMethods.Head));
        Assert.False(InvokeShouldValidate(route, HttpMethods.Options));
    }

    [Fact]
    public void Post_TriggersValidation()
    {
        var route = MixedFormRoute();
        Assert.True(InvokeShouldValidate(route, HttpMethods.Post));
    }

    [Fact]
    public void Put_Patch_Delete_TriggerValidation_WhenConfigured()
    {
        var route = MixedFormRoute() with { HttpVerbs = [HttpVerb.Get, HttpVerb.Put, HttpVerb.Patch, HttpVerb.Delete] };
        Assert.True(InvokeShouldValidate(route, HttpMethods.Put));
        Assert.True(InvokeShouldValidate(route, HttpMethods.Patch));
        Assert.True(InvokeShouldValidate(route, HttpMethods.Delete));
    }

    [Fact]
    public void UnsafeVerbNotConfigured_DoesNotTrigger()
    {
        var route = MixedFormRoute() with { HttpVerbs = [HttpVerb.Get] }; // only GET configured
        Assert.False(InvokeShouldValidate(route, HttpMethods.Post)); // POST not part of route
    }

    [Fact]
    public void DisabledAntiforgery_SuppressesValidation()
    {
        var route = MixedFormRoute(disable: true);
        Assert.False(InvokeShouldValidate(route, HttpMethods.Post));
    }
}

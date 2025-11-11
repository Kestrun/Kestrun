using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.IncludeScopes = true;
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss.fff ";
});
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Crank up only what we care about
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.Cookies", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.OpenIdConnect", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.IdentityModel", LogLevel.Debug);          // token validation 
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Trace);
 
// AuthN/Z
builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = "oidc";
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, opts =>
    {
        opts.SlidingExpiration = true;
    })
    .AddOpenIdConnect("oidc", opts =>
    {
        // Duende demo
        opts.Authority = "https://demo.duendesoftware.com";
        opts.ClientId = "interactive.confidential";
        opts.ClientSecret = "secret";

        // Code + PKCE
        opts.ResponseType = OpenIdConnectResponseType.Code;
        opts.UsePkce = true;
        opts.ResponseMode = OpenIdConnectResponseMode.FormPost;
        opts.RequireHttpsMetadata = true;

        // Tokens & userinfo
        opts.SaveTokens = true;
        opts.GetClaimsFromUserInfoEndpoint = true;
     //   opts.SignedOutCallbackPath = "/signout-callback-oidc";
        // optional: where to land after the callback completes
        opts.SignedOutRedirectUri = "/";

        // Scopes
        opts.Scope.Clear();
        opts.Scope.Add("openid");
        opts.Scope.Add("profile");
        opts.Scope.Add("email");
        opts.Scope.Add("offline_access");
        opts.Scope.Add("api");

        // Claims mapping
        opts.MapInboundClaims = false;
        opts.TokenValidationParameters.NameClaimType = "name";
        opts.TokenValidationParameters.RoleClaimType = "roles";

        // Link to cookies
        opts.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

//app.UseHttpsRedirection();
//app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Content("""
    <html>
        <body>
            <h2>Duende OIDC (ASP.NET Core)</h2>
            <ul>
                <li><a href="/login">Login</a></li>
                <li><a href="/hello">Hello (requires auth)</a></li>
                <li><a href="/tokens">Show token snippets (requires auth)</a></li>
                <li><a href="/logout">Logout</a></li>
            </ul>
        </body>
    </html>
    """, "text/html")
).AllowAnonymous();

app.MapGet("/login", async (HttpContext ctx) =>
{
    var props = new AuthenticationProperties { RedirectUri = "/hello" };
    await ctx.ChallengeAsync("oidc", props);
}).AllowAnonymous();

app.MapGet("/hello", (ClaimsPrincipal user) =>
{
    var name = user.Identity?.Name ?? "(no name)";
    return Results.Content($"""
        <html>
            <body>
                <h3>Hello, {System.Net.WebUtility.HtmlEncode(name)}!</h3>
                <a href="/">Home</a>
            </body>
        </html>
        """, "text/html");
}).RequireAuthorization();

app.MapGet("/tokens", async (HttpContext ctx) =>
{
    var access = await ctx.GetTokenAsync("access_token") ?? "(none)";
    var id = await ctx.GetTokenAsync("id_token") ?? "(none)";
    var refresh = await ctx.GetTokenAsync("refresh_token") ?? "(none)";

    static string Clip(string s, int n = 60) => s.Length <= n ? s : s[..n] + "...";

    return Results.Content($"""
        <html>
            <body>
                <h3>Tokens</h3>
                <pre>
                    Access:  {System.Net.WebUtility.HtmlEncode(Clip(access))}
                    ID:      {System.Net.WebUtility.HtmlEncode(Clip(id))}
                    Refresh: {System.Net.WebUtility.HtmlEncode(refresh)}
                </pre>
                <a href="/">Home</a>
            </body>
        </html>
        """, "text/html");
}).RequireAuthorization();

app.MapGet("/tokens2", async (HttpContext context) =>
{
    var authenticateResult = await context.AuthenticateAsync("Cookies");
    if (authenticateResult.Succeeded)
    {
        var tokens = new
        {
            id_token = await context.GetTokenAsync("id_token"),
            access_token = await context.GetTokenAsync("access_token"),
            refresh_token = await context.GetTokenAsync("refresh_token"),
            token_type = await context.GetTokenAsync("token_type")
        };
        return Results.Json(tokens);
    }
    return Results.Unauthorized();
}).RequireAuthorization();

app.MapGet("/logout", async (HttpContext ctx) =>
{
      // 1) Clear your app's auth cookie
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    // 2) Trigger RP-initiated logout at the IdP
    // The OIDC handler will send id_token_hint (because SaveTokens=true)
    // and use SignedOutCallbackPath for the post_logout_redirect_uri.
    await ctx.SignOutAsync("oidc", new AuthenticationProperties
    {
        // After the OIDC handler receives the /signout-callback-oidc response,
        // it will redirect here:
        RedirectUri = "/"
    });
}).RequireAuthorization();

app.Run();

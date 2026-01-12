using System.Security.Claims;
using Kestrun.Models;
using Kestrun.Hosting;
using Kestrun.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace KestrunTests.Models;

public class KestrunContextTests
{
    private static KestrunContext NewContext(DefaultHttpContext http)
    {
        var host = new KestrunHost("Tests", AppContext.BaseDirectory);

        return NewContext(host, http);
    }

    private static KestrunContext NewContext(KestrunHost host, DefaultHttpContext http)
    {

        if (string.IsNullOrWhiteSpace(http.Request.Method))
        {
            http.Request.Method = "GET";
        }

        // KestrunContext(host, HttpContext) requires a RouteEndpoint.
        http.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(http.Request.Path.HasValue ? http.Request.Path.Value : "/"), 0, EndpointMetadataCollection.Empty, "TestEndpoint"));

        return new KestrunContext(host, http);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Constructor_WhenNoEndpoint_FallsBackToRootPattern()
    {
        var host = new KestrunHost("Tests", AppContext.BaseDirectory);
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/api/anything";

        // Intentionally do NOT set an endpoint.
        var ctx = new KestrunContext(host, http);

        Assert.Equal("/", ctx.MapRouteOptions.Pattern);
        Assert.Contains(HttpVerb.Post, ctx.MapRouteOptions.HttpVerbs);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Constructor_WhenRouteRegistered_UsesRegisteredMapRouteOptions()
    {
        var host = new KestrunHost("Tests", AppContext.BaseDirectory);

        var registered = new Kestrun.Hosting.Options.MapRouteOptions
        {
            Pattern = "/api/registered",
            HttpVerbs = [HttpVerb.Get]
        };
        host.RegisteredRoutes[("/api/registered", HttpVerb.Get)] = registered;

        var http = new DefaultHttpContext();
        http.Request.Method = "GET";
        http.Request.Path = "/api/registered";
        http.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/api/registered"),
            order: 0,
            metadata: EndpointMetadataCollection.Empty,
            displayName: "TestEndpoint"));

        var ctx = NewContext(host, http);
        Assert.Same(registered, ctx.MapRouteOptions);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Session_absent_returns_null_and_flags_false()
    {
        var http = new DefaultHttpContext();
        var ctx = NewContext(http);

        Assert.Null(ctx.Session);
        Assert.False(ctx.HasSession);

        Assert.False(ctx.TryGetSession(out var s));
        Assert.Null(s);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Session_present_returns_instance_and_flags_true()
    {
        var http = new DefaultHttpContext();
        var session = new TestSession();
        http.Features.Set<ISessionFeature>(new TestSessionFeature(session));

        var ctx = NewContext(http);

        Assert.Same(session, ctx.Session);
        Assert.True(ctx.HasSession);

        Assert.True(ctx.TryGetSession(out var s));
        Assert.Same(session, s);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void Items_pass_through_dictionary()
    {
        var http = new DefaultHttpContext();
        http.Items["foo"] = 123;
        var ctx = NewContext(http);

        Assert.Same(http.Items, ctx.Items);
        Assert.True(ctx.Items.ContainsKey("foo"));
        Assert.Equal(123, ctx.Items["foo"]);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void User_pass_through_and_ToString_includes_path_user_and_session_flag()
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/test";
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice")], "test");
        http.User = new ClaimsPrincipal(identity);

        // with no session feature
        var ctx1 = NewContext(http);
        var s1 = ctx1.ToString();
        Assert.Contains("Path=/api/test", s1);
        Assert.Contains("User=alice", s1);
        Assert.Contains("HasSession=False", s1, StringComparison.OrdinalIgnoreCase);

        // with a session feature
        var session = new TestSession();
        http.Features.Set<ISessionFeature>(new TestSessionFeature(session));
        var ctx2 = NewContext(http);
        var s2 = ctx2.ToString();
        Assert.Contains("HasSession=True", s2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public void CancellationToken_pass_through()
    {
        var http = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        http.RequestAborted = cts.Token;

        var ctx = NewContext(http);
        Assert.Equal(cts.Token, ctx.Ct);
    }

    private sealed class TestSessionFeature(ISession session) : ISessionFeature
    {
        public ISession Session { get; set; } = session;
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.OrdinalIgnoreCase);

        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _store.Keys;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}

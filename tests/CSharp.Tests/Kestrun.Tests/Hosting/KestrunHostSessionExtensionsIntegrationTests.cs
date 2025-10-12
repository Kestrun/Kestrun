using System.Net;
using Kestrun.Hosting;
using Serilog;
using Xunit;
using Microsoft.AspNetCore.Builder;
using Kestrun.Scripting;
using Kestrun.Utilities;

namespace KestrunTests.Hosting;

/// <summary>
/// Integration tests for session middleware and distributed memory cache using a live in-process server.
/// </summary>
public class KestrunHostSessionExtensionsIntegrationTests
{
    private static KestrunHost CreateBuiltHost(Action<KestrunHost>? configure = null)
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestSession", logger, AppContext.BaseDirectory);
        host.ConfigureListener(0, IPAddress.Loopback, useConnectionLogging: false);
        configure?.Invoke(host);
        host.EnableConfiguration();
        return host;
    }

    private static async Task<(KestrunHost host, HttpClient client, int port)> StartAsync(KestrunHost host, CancellationToken ct)
    {
        await host.StartAsync(ct);
        var appField = typeof(KestrunHost).GetField("_app", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var app = (WebApplication)appField.GetValue(host)!;
        var port = app.Urls.Select(u => new Uri(u).Port).First();
        var client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/") };
        return (host, client, port);
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task Session_State_Persists_Across_Requests_For_Same_Client()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Configure distributed cache (required by sessions) and sessions before building the app
        var host = CreateBuiltHost(h =>
        {
            _ = h.AddDistributedMemoryCache(null!);
            _ = h.AddSession(null!);
        });

        // Map a route that increments a session counter and returns the current value as text
        _ = host.AddMapRoute("/inc", HttpVerb.Get, @"
            var key = ""count"";
            byte[] bytes;
            var val = 0;
            if (Context.Session.TryGetValue(key, out bytes))
            {
                var s = System.Text.Encoding.UTF8.GetString(bytes);
                int.TryParse(s, out val);
            }
            val++;
            Context.Session.Set(key, System.Text.Encoding.UTF8.GetBytes(val.ToString()));
            Context.Response.WriteTextResponse(val.ToString());
        ", ScriptLanguage.CSharp);

        try
        {
            var (_, client, _) = await StartAsync(host, cts.Token);

            // Same client should see incrementing values as cookie is preserved
            var r1 = await client.GetStringAsync("inc", cts.Token);
            Assert.Equal("1", r1);
            var r2 = await client.GetStringAsync("inc", cts.Token);
            Assert.Equal("2", r2);

            // A new client without prior cookies should start from 1
            using var fresh = new HttpClient { BaseAddress = client.BaseAddress };
            var rNew = await fresh.GetStringAsync("inc", cts.Token);
            Assert.Equal("1", rNew);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }

    [Fact]
    [Trait("Category", "Hosting")]
    public async Task DistributedMemoryCache_Can_Store_And_Retrieve_Via_DI()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Only register the distributed memory cache
        var host = CreateBuiltHost(h =>
        {
            _ = h.AddDistributedMemoryCache(null!);
        });

        // Route uses DI to resolve IDistributedCache, writes and reads a value
        _ = host.AddMapRoute("/cache", HttpVerb.Get, @"
            var cache = (Microsoft.Extensions.Caching.Distributed.IDistributedCache)Context.HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache));
            if (cache == null) { Context.Response.StatusCode = 500; Context.Response.WriteTextResponse(""nocache""); return; }
            var key = ""k""; var value = ""v"";
            Microsoft.Extensions.Caching.Distributed.DistributedCacheExtensions.SetString(cache, key, value);
            var result = Microsoft.Extensions.Caching.Distributed.DistributedCacheExtensions.GetString(cache, key);
            Context.Response.WriteTextResponse(result ?? ""null"");
        ", ScriptLanguage.CSharp);

        try
        {
            var (_, client, _) = await StartAsync(host, cts.Token);
            var resp = await client.GetAsync("cache", cts.Token);
            _ = resp.EnsureSuccessStatusCode();
            var text = await resp.Content.ReadAsStringAsync(cts.Token);
            Assert.Equal("v", text);
        }
        finally
        {
            await host.StopAsync(cts.Token);
        }
    }
}

using System.Reflection;
using Kestrun.Mcp;
using Kestrun.Mcp.ServerHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var options = KestrunMcpCommandLine.Parse(args);
if (options is null)
{
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(console =>
{
    console.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<KestrunMcpRuntime>();
builder.Services.AddSingleton<IKestrunRouteInspector, KestrunRouteInspector>();
builder.Services.AddSingleton<IKestrunOpenApiProvider, KestrunOpenApiProvider>();
builder.Services.AddSingleton<IKestrunRuntimeInspector, KestrunRuntimeInspector>();
builder.Services.AddSingleton<IKestrunRequestValidator, KestrunRequestValidator>();
builder.Services.AddSingleton(new KestrunRequestInvokerOptions
{
    EnableInvocation = options.AllowInvokeRoute,
    AllowedPathPatterns = options.AllowedInvokePaths
});
builder.Services.AddSingleton<IKestrunRequestInvoker, KestrunRequestInvoker>();
builder.Services.AddHostedService<KestrunScriptSessionHostedService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(Assembly.GetExecutingAssembly());

await builder.Build().RunAsync();
return 0;

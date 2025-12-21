using System.Reflection;
using Kestrun.Razor;
using Kestrun.Scripting;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;
using Serilog.Events;

namespace Kestrun.Hosting;

/// <summary>
/// Provides extension methods for adding PowerShell and Razor Pages to a Kestrun
/// </summary>
public partial class KestrunHost
{
    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// This middleware allows you to serve Razor Pages using PowerShell scripts.
    /// </summary>
    /// <param name="rootPath">The root directory for the Razor Pages.</param>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <param name="cfg">Configuration options for the Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages(string? rootPath, PathString? routePrefix, RazorPagesOptions? cfg)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding PowerShell Razor Pages with route prefix: {RoutePrefix}, config: {@Config}", routePrefix, cfg);
        }

        return AddPowerShellRazorPages(rootPath, routePrefix, dest =>
            {
                if (cfg != null)
                {
                    // simple value properties are fine
                    dest.RootDirectory = cfg.RootDirectory;

                    // copy conventions one‑by‑one (collection is read‑only)
                    foreach (var c in cfg.Conventions)
                    {
                        dest.Conventions.Add(c);
                    }
                }
            });
    }

    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// This middleware allows you to serve Razor Pages using PowerShell scripts.
    /// </summary>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages(PathString? routePrefix) =>
        AddPowerShellRazorPages(rootPath: null, routePrefix: routePrefix, cfg: null as RazorPagesOptions);

    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// </summary>
    /// <param name="rootPath">The root directory for the Razor Pages.</param>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages(string? rootPath, PathString? routePrefix) =>
            AddPowerShellRazorPages(rootPath: rootPath, routePrefix: routePrefix, cfg: null as RazorPagesOptions);

    /// <summary>
    /// Adds PowerShell Razor Pages to the application with default configuration and no route prefix.
    /// </summary>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages() =>
        AddPowerShellRazorPages(rootPath: null, routePrefix: null, cfg: null as RazorPagesOptions);

    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// </summary>
    /// <param name="rootPath">The root directory for the Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages(string? rootPath) =>
        AddPowerShellRazorPages(rootPath: rootPath, routePrefix: null, cfg: null as RazorPagesOptions);

    /// <summary>
    /// Adds PowerShell Razor Pages to the application.
    /// This middleware allows you to serve Razor Pages using PowerShell scripts.
    /// </summary>
    /// <param name="rootPath">The root directory for the Razor Pages.</param>
    /// <param name="routePrefix">The route prefix to use for the PowerShell Razor Pages.</param>
    /// <param name="cfg">Configuration options for the Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddPowerShellRazorPages(string? rootPath, PathString? routePrefix, Action<RazorPagesOptions>? cfg = null)
    {
        LogAddPowerShellRazorPages(routePrefix, cfg);

        var env = Builder.Environment;
        var isDefaultPath = string.IsNullOrWhiteSpace(rootPath);
        var pagesRootPath = ResolvePagesRootPath(env.ContentRootPath, rootPath, isDefaultPath);
        var rootDirectory = ResolveRazorRootDirectory(env.ContentRootPath, pagesRootPath, isDefaultPath);

        _ = AddService(services =>
        {
            LogAddPowerShellRazorPagesService(routePrefix);

            var mvcBuilder = ConfigureRazorPages(services, rootDirectory, cfg);
            _ = mvcBuilder.AddRazorRuntimeCompilation();

            ConfigureRuntimeCompilationReferences(services, pagesRootPath);
        });

        return Use(app =>
        {
            ArgumentNullException.ThrowIfNull(RunspacePool);
            LogAddPowerShellRazorPagesMiddleware(routePrefix);

            MapPowerShellRazorPages(app, RunspacePool, pagesRootPath, routePrefix);

            LogAddPowerShellRazorPagesMiddlewareAdded(routePrefix);
        });
    }

    /// <summary>
    /// Logs that PowerShell Razor Pages are being added.
    /// </summary>
    /// <param name="routePrefix">Optional route prefix for mounting Razor Pages.</param>
    /// <param name="cfg">Optional Razor Pages configuration delegate.</param>
    private void LogAddPowerShellRazorPages(PathString? routePrefix, Action<RazorPagesOptions>? cfg)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding PowerShell Razor Pages with route prefix: {RoutePrefix}, config: {@Config}", routePrefix, cfg);
        }
    }

    /// <summary>
    /// Logs that PowerShell Razor Pages services are being added.
    /// </summary>
    /// <param name="routePrefix">Optional route prefix for mounting Razor Pages.</param>
    private void LogAddPowerShellRazorPagesService(PathString? routePrefix)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding PowerShell Razor Pages to the service with route prefix: {RoutePrefix}", routePrefix);
        }
    }

    /// <summary>
    /// Logs that PowerShell Razor Pages middleware is being added.
    /// </summary>
    /// <param name="routePrefix">Optional route prefix for mounting Razor Pages.</param>
    private void LogAddPowerShellRazorPagesMiddleware(PathString? routePrefix)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding PowerShell Razor Pages middleware with route prefix: {RoutePrefix}", routePrefix);
        }
    }

    /// <summary>
    /// Logs that PowerShell Razor Pages middleware has been added.
    /// </summary>
    /// <param name="routePrefix">Optional route prefix for mounting Razor Pages.</param>
    private void LogAddPowerShellRazorPagesMiddlewareAdded(PathString? routePrefix)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("PowerShell Razor Pages middleware added with route prefix: {RoutePrefix}", routePrefix);
        }
    }

    /// <summary>
    /// Resolves the filesystem path used as the Pages root.
    /// </summary>
    /// <param name="contentRootPath">The application content root path.</param>
    /// <param name="rootPath">Optional explicit Pages root path.</param>
    /// <param name="isDefaultPath">Whether <paramref name="rootPath"/> was not provided.</param>
    /// <returns>The resolved Pages root path.</returns>
    private string ResolvePagesRootPath(string contentRootPath, string? rootPath, bool isDefaultPath)
    {
        return isDefaultPath
            ? Path.Combine(contentRootPath, "Pages")
            : rootPath!;
    }

    /// <summary>
    /// Resolves the Razor Pages <see cref="RazorPagesOptions.RootDirectory"/> value.
    /// </summary>
    /// <param name="contentRootPath">The application content root path.</param>
    /// <param name="pagesRootPath">The resolved Pages root filesystem path.</param>
    /// <param name="isDefaultPath">Whether the Pages root is the default path.</param>
    /// <returns>The RootDirectory value to apply, or <c>null</c> if no override should be applied.</returns>
    private string? ResolveRazorRootDirectory(string contentRootPath, string pagesRootPath, bool isDefaultPath)
    {
        if (isDefaultPath)
        {
            return null;
        }

        var relative = Path.GetRelativePath(contentRootPath, pagesRootPath)
            .Replace("\\", "/");
        return "/" + relative;
    }

    /// <summary>
    /// Configures Razor Pages and applies optional RootDirectory and user configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="rootDirectory">Optional Razor Pages root directory (virtual path) to apply.</param>
    /// <param name="cfg">Optional user configuration delegate for Razor Pages options.</param>
    /// <returns>The MVC builder instance.</returns>
    private static IMvcBuilder ConfigureRazorPages(IServiceCollection services, string? rootDirectory, Action<RazorPagesOptions>? cfg)
    {
        var mvcBuilder = services.AddRazorPages();

        if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            _ = mvcBuilder.AddRazorPagesOptions(opts => opts.RootDirectory = rootDirectory);
        }

        if (cfg != null)
        {
            _ = mvcBuilder.AddRazorPagesOptions(cfg);
        }

        return mvcBuilder;
    }
#pragma warning disable ASPDEPR003
    /// <summary>
    /// Configures runtime compilation reference paths and optional file watching for the Pages directory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pagesRootPath">The resolved Pages directory path.</param>
    private void ConfigureRuntimeCompilationReferences(IServiceCollection services, string pagesRootPath)
    {
        _ = services.Configure<MvcRazorRuntimeCompilationOptions>(opts =>
        {
            AddLoadedAssemblyReferences(opts);
            AddSharedFrameworkReferences(opts);
            AddPagesFileProviderIfExists(opts, pagesRootPath);
        });
    }

    /// <summary>
    /// Adds already-loaded managed assemblies as Roslyn reference paths.
    /// </summary>
    /// <param name="opts">Runtime compilation options to update.</param>
    private void AddLoadedAssemblyReferences(MvcRazorRuntimeCompilationOptions opts)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && IsManaged(a.Location)))
        {
            opts.AdditionalReferencePaths.Add(asm.Location);
        }
    }

    /// <summary>
    /// Adds managed DLLs from the shared framework directory as Roslyn reference paths.
    /// </summary>
    /// <param name="opts">Runtime compilation options to update.</param>
    private void AddSharedFrameworkReferences(MvcRazorRuntimeCompilationOptions opts)
    {
        var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var dll in Directory.EnumerateFiles(coreDir, "*.dll").Where(IsManaged))
        {
            opts.AdditionalReferencePaths.Add(dll);
        }
    }

    /// <summary>
    /// Adds a file provider for the Pages directory so Razor runtime compilation can watch changes.
    /// </summary>
    /// <param name="opts">Runtime compilation options to update.</param>
    /// <param name="pagesRootPath">The resolved Pages directory path.</param>
    private static void AddPagesFileProviderIfExists(MvcRazorRuntimeCompilationOptions opts, string pagesRootPath)
    {
        if (Directory.Exists(pagesRootPath))
        {
            opts.FileProviders.Add(new PhysicalFileProvider(pagesRootPath));
        }
    }
#pragma warning restore ASPDEPR003
    /// <summary>
    /// Maps PowerShell Razor Pages middleware either at the application root or under a route prefix.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="pool">The runspace pool manager for PowerShell execution.</param>
    /// <param name="pagesRootPath">The resolved Pages directory path.</param>
    /// <param name="routePrefix">Optional route prefix for mounting Razor Pages.</param>
    private void MapPowerShellRazorPages(IApplicationBuilder app, KestrunRunspacePoolManager pool, string pagesRootPath, PathString? routePrefix)
    {
        if (routePrefix.HasValue)
        {
            _ = app.Map(routePrefix.Value, branch =>
            {
                _ = branch.UsePowerShellRazorPages(pool, pagesRootPath);
                _ = branch.UseRouting();
                _ = branch.UseEndpoints(e => e.MapRazorPages());
            });

            return;
        }

        _ = app.UsePowerShellRazorPages(pool, pagesRootPath);
        _ = app.UseRouting();
        _ = app.UseEndpoints(e => e.MapRazorPages());
    }

    /// <summary>
    /// Adds Razor Pages to the application.
    /// </summary>
    /// <param name="cfg">The configuration options for Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddRazorPages(RazorPagesOptions? cfg)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding Razor Pages from source: {Source}", cfg);
        }

        if (cfg == null)
        {
            return AddRazorPages(); // no config, use defaults
        }

        return AddRazorPages(dest =>
            {
                // simple value properties are fine
                dest.RootDirectory = cfg.RootDirectory;

                // copy conventions one‑by‑one (collection is read‑only)
                foreach (var c in cfg.Conventions)
                {
                    dest.Conventions.Add(c);
                }
            });
    }

    /// <summary>
    /// Adds Razor Pages to the application.
    /// This overload allows you to specify configuration options.
    /// If you need to configure Razor Pages options, use the other overload.
    /// </summary>
    /// <param name="cfg">The configuration options for Razor Pages.</param>
    /// <returns>The current KestrunHost instance.</returns>
    public KestrunHost AddRazorPages(Action<RazorPagesOptions>? cfg = null)
    {
        if (Logger.IsEnabled(LogEventLevel.Debug))
        {
            Logger.Debug("Adding Razor Pages with configuration: {Config}", cfg);
        }

        return AddService(services =>
        {
            var mvc = services.AddRazorPages();         // returns IMvcBuilder

            if (cfg != null)
            {
                _ = mvc.AddRazorPagesOptions(cfg);          // ← the correct extension
            }
            //  —OR—
            // services.Configure(cfg);                 // also works
        })
         .Use(app => ((IEndpointRouteBuilder)app).MapRazorPages());// optional: automatically map Razor endpoints after Build()
    }

    // helper: true  ⇢ file contains managed metadata
    private bool IsManaged(string path)
    {
        try { _ = AssemblyName.GetAssemblyName(path); return true; }
        catch { return false; }          // native ⇒ BadImageFormatException
    }
}

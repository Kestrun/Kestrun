namespace Kestrun.Callback;

/// <summary>
/// Registers callback-related services in the dependency injection container.
/// </summary>
internal static class CallbackServiceRegistration
{
    /// <summary>
    /// Registers callback-related services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The updated service collection.</returns>
    internal static IServiceCollection RegisterServices(IServiceCollection services)
    {
        _ = services.AddSingleton<InMemoryCallbackQueue>();
        _ = services.AddSingleton<ICallbackDispatcher, InMemoryCallbackDispatcher>();
        _ = services.AddHostedService<InMemoryCallbackDispatchWorker>();
        _ = services.AddHttpClient("kestrun-callbacks", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        _ = services.AddSingleton<ICallbackRetryPolicy, DefaultCallbackRetryPolicy>();

        _ = services.AddSingleton<ICallbackUrlResolver, DefaultCallbackUrlResolver>();
        _ = services.AddSingleton<ICallbackBodySerializer, JsonCallbackBodySerializer>();

        _ = services.AddHttpClient<ICallbackSender, HttpCallbackSender>();

        _ = services.AddHostedService<CallbackWorker>();
        return services;
    }
}

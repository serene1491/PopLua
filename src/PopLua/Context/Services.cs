namespace PopLua.Context;

/// <summary>
/// Tiny exact-type service provider for small hosts and examples.
/// </summary>
/// <remarks>
/// Services are looked up by exact registered type. Register an interface type
/// explicitly, for example <c>Add&lt;IMyService&gt;(service)</c>, when generated
/// instance-module constructors request that interface. PopLua does not dispose
/// service instances.
/// </remarks>
public sealed class Services : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = [];

    private Services()
    {
    }

    internal static Services Empty { get; } = new();

    /// <summary>
    /// Creates an empty exact-type service provider for generated bindings.
    /// </summary>
    /// <returns>A mutable service collection that also implements <see cref="IServiceProvider"/>.</returns>
    public static Services Create() => new();

    /// <summary>
    /// Adds or replaces a service instance keyed by its exact compile-time type.
    /// </summary>
    /// <typeparam name="T">Exact service type used as the lookup key.</typeparam>
    /// <param name="instance">Service instance owned by the host application.</param>
    /// <returns>The current service collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is <see langword="null"/>.</exception>
    public Services Add<T>(T instance)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(instance);

        _services[typeof(T)] = instance;
        return this;
    }

    /// <summary>
    /// Gets a service registered by exact type, or <see langword="null"/> when it is unavailable.
    /// </summary>
    /// <param name="serviceType">Exact service type to look up.</param>
    /// <returns>The registered instance, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceType"/> is <see langword="null"/>.</exception>
    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return _services.TryGetValue(serviceType, out var service) ? service : null;
    }
}

namespace PopLua.Runtime;

/// <summary>
/// Builds immutable Lua runtimes.
/// </summary>
/// <remarks>
/// Builder methods configure defaults used by sessions created from the
/// resulting <see cref="Engine"/>. Session-specific services can still be
/// supplied to <see cref="Engine.Session"/>.
/// </remarks>
public sealed class EngineBuilder
{
    private readonly ModuleCollection _modules = new();
    private IServiceProvider? _services;
    private IDiagnostics _diagnostics = NullDiagnostics.Instance;
    private AllocatorOptions _allocator;
    private ModuleResolver? _moduleResolver;

    /// <summary>
    /// Adds a source-generated Lua module to runtimes built by this builder.
    /// </summary>
    /// <typeparam name="T">Source type marked with <see cref="ModuleAttribute"/>.</typeparam>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Use <see cref="Modules{T1,T2}"/> and the higher-arity overloads when a
    /// runtime exposes several generated modules.
    /// </remarks>
    public EngineBuilder Module<T>()
    {
        _modules.Add<T>();
        return this;
    }

    /// <summary>
    /// Adds two source-generated Lua modules to runtimes built by this builder.
    /// </summary>
    /// <typeparam name="T1">First generated module type.</typeparam>
    /// <typeparam name="T2">Second generated module type.</typeparam>
    /// <returns>The current builder.</returns>
    public EngineBuilder Modules<T1, T2>()
    {
        _modules.Add<T1>();
        _modules.Add<T2>();
        return this;
    }

    /// <summary>
    /// Adds three source-generated Lua modules to runtimes built by this builder.
    /// </summary>
    /// <typeparam name="T1">First generated module type.</typeparam>
    /// <typeparam name="T2">Second generated module type.</typeparam>
    /// <typeparam name="T3">Third generated module type.</typeparam>
    /// <returns>The current builder.</returns>
    public EngineBuilder Modules<T1, T2, T3>()
    {
        _modules.Add<T1>();
        _modules.Add<T2>();
        _modules.Add<T3>();
        return this;
    }

    /// <summary>
    /// Adds four source-generated Lua modules to runtimes built by this builder.
    /// </summary>
    public EngineBuilder Modules<T1, T2, T3, T4>()
    {
        _modules.Add<T1>();
        _modules.Add<T2>();
        _modules.Add<T3>();
        _modules.Add<T4>();
        return this;
    }

    /// <summary>
    /// Adds five source-generated Lua modules to runtimes built by this builder.
    /// </summary>
    public EngineBuilder Modules<T1, T2, T3, T4, T5>()
    {
        _modules.Add<T1>();
        _modules.Add<T2>();
        _modules.Add<T3>();
        _modules.Add<T4>();
        _modules.Add<T5>();
        return this;
    }

    /// <summary>
    /// Adds six source-generated Lua modules to runtimes built by this builder.
    /// </summary>
    public EngineBuilder Modules<T1, T2, T3, T4, T5, T6>()
    {
        _modules.Add<T1>();
        _modules.Add<T2>();
        _modules.Add<T3>();
        _modules.Add<T4>();
        _modules.Add<T5>();
        _modules.Add<T6>();
        return this;
    }

    /// <summary>
    /// Adds seven source-generated Lua modules to runtimes built by this builder.
    /// </summary>
    public EngineBuilder Modules<T1, T2, T3, T4, T5, T6, T7>()
    {
        _modules.Add<T1>();
        _modules.Add<T2>();
        _modules.Add<T3>();
        _modules.Add<T4>();
        _modules.Add<T5>();
        _modules.Add<T6>();
        _modules.Add<T7>();
        return this;
    }

    /// <summary>
    /// Adds eight source-generated Lua modules to runtimes built by this builder.
    /// </summary>
    public EngineBuilder Modules<T1, T2, T3, T4, T5, T6, T7, T8>()
    {
        _modules.Add<T1>();
        _modules.Add<T2>();
        _modules.Add<T3>();
        _modules.Add<T4>();
        _modules.Add<T5>();
        _modules.Add<T6>();
        _modules.Add<T7>();
        _modules.Add<T8>();
        return this;
    }

    /// <summary>
    /// Configures the generated module collection using a callback.
    /// </summary>
    /// <param name="add">Callback that adds or removes generated modules.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="add"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// var lua = Engine.Create(b => b.Modules&lt;ApiModule, LogModule&gt;());
    /// </code>
    /// </example>
    public EngineBuilder Modules(Action<ModuleCollection> add)
    {
        ArgumentNullException.ThrowIfNull(add);

        add(_modules);
        return this;
    }

    /// <summary>
    /// Sets the default service provider used by new sessions and generated bindings.
    /// </summary>
    /// <param name="services">Default services used when a session does not provide its own service provider.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// PopLua does not dispose services. The host application owns their lifetime.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public EngineBuilder Services(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        return this;
    }

    /// <summary>
    /// Sets the diagnostics sink used by sessions created from the runtime.
    /// </summary>
    /// <param name="diagnostics">Diagnostics sink that receives execution events.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostics"/> is <see langword="null"/>.</exception>
    public EngineBuilder Diagnostics(IDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        _diagnostics = diagnostics;
        return this;
    }

    /// <summary>
    /// Sets allocator options used when creating Lua states for new sessions.
    /// </summary>
    /// <param name="options">Allocator defaults for sessions created by the runtime.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Sandbox memory settings take precedence for sessions that configure
    /// memory quotas directly.
    /// </remarks>
    public EngineBuilder Allocator(AllocatorOptions options)
    {
        _allocator = options;
        return this;
    }

    /// <summary>
    /// Enables controlled Lua <c>require</c> using a host-provided resolver.
    /// </summary>
    /// <param name="resolver">Resolver that maps normalized module names to approved Lua chunks.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// The resolver is the explicit opt-in switch for PopLua's controlled
    /// loader. It does not use Lua's filesystem search path, <c>package.path</c>,
    /// or <c>package.cpath</c>. Sessions can override the runtime resolver by
    /// providing a <see cref="ModuleResolver"/> service. Returning
    /// <see langword="null"/> reports the module as not found; throwing from the
    /// resolver reports a resolver failure through the active execution.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resolver"/> is <see langword="null"/>.</exception>
    public EngineBuilder Require(ModuleResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        _moduleResolver = resolver;
        return this;
    }

    internal Engine Build()
        => new(_modules.Modules.ToArray(), _services, _diagnostics, _allocator, _moduleResolver);
}

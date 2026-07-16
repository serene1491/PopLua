namespace PopLua.Runtime;

/// <summary>
/// Immutable Lua host runtime. Share one instance across the host application.
/// </summary>
/// <remarks>
/// A runtime stores generated module registrations, default services,
/// diagnostics, and allocator settings. It does not own a Lua state; each
/// <see cref="Session"/> owns one state and must be disposed separately.
/// </remarks>
public sealed class Engine : IAsyncDisposable
{
    private readonly IReadOnlyList<ModuleDescriptor> _modules;
    private readonly IServiceProvider? _services;
    private readonly IDiagnostics _diagnostics;
    private readonly AllocatorOptions _allocator;
    private readonly ModuleResolver? _moduleResolver;

    internal Engine(
        IReadOnlyList<ModuleDescriptor> modules,
        IServiceProvider? services,
        IDiagnostics diagnostics,
        AllocatorOptions allocator,
        ModuleResolver? moduleResolver)
    {
        _modules = modules;
        _services = services;
        _diagnostics = diagnostics;
        _allocator = allocator;
        _moduleResolver = moduleResolver;
    }

    internal IReadOnlyList<ModuleDescriptor> Modules => _modules;
    internal IServiceProvider? Services => _services;
    internal IDiagnostics Diagnostics => _diagnostics;
    internal AllocatorOptions Allocator => _allocator;
    internal ModuleResolver? ModuleResolver => _moduleResolver;

    /// <summary>
    /// Gets the native Lua language version selected for the current process.
    /// </summary>
    public Language Language => LibraryResolver.Version;

    /// <summary>
    /// Creates a runtime with optional modules, services, diagnostics, and allocator settings.
    /// </summary>
    /// <param name="configure">Optional callback used to register modules, services, diagnostics, and allocator settings.</param>
    /// <returns>An immutable runtime that can create independent Lua sessions.</returns>
    /// <example>
    /// <code>
    /// var lua = Engine.Create(b => b.Modules&lt;ApiModule, LogModule&gt;());
    /// </code>
    /// </example>
    public static Engine Create(Action<EngineBuilder>? configure = null)
    {
        var builder = new EngineBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// Creates a stateful Lua session. Sessions are not thread-safe and must be disposed when no longer needed.
    /// </summary>
    /// <param name="sandbox">Sandbox policy for the session, or <see cref="Sandbox.Untrusted"/> when omitted.</param>
    /// <param name="identity">Host-defined script identity, or <see cref="Identity.Anonymous"/> when omitted.</param>
    /// <param name="services">Session service provider. When omitted, the runtime default services are used.</param>
    /// <returns>A new session with its own Lua state.</returns>
    /// <remarks>
    /// Create a fresh session for isolated script executions. Reuse a session
    /// only when Lua globals should intentionally survive between calls.
    /// </remarks>
    public Session Session(
        Sandbox? sandbox = null,
        Identity? identity = null,
        IServiceProvider? services = null)
        => new(
            this,
            sandbox ?? Sandbox.Untrusted,
            identity ?? Identity.Anonymous,
            services ?? _services ?? PopLua.Context.Services.Empty
        );

    /// <summary>
    /// Runs source text in a temporary session and returns the first Lua value or an execution error.
    /// </summary>
    /// <param name="code">Lua source text encoded as UTF-16 in the host application.</param>
    /// <param name="sandbox">Sandbox policy for the temporary session, or <see cref="Sandbox.Untrusted"/> when omitted.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A result containing the first Lua return value, or a PopLua error.</returns>
    /// <remarks>
    /// This is a convenience API for small uses. Scripting platforms should
    /// usually create named <see cref="Chunk"/> values, compile submitted
    /// scripts, and run the resulting <see cref="Bytecode"/> in fresh
    /// sessions so diagnostics can identify the original script.
    /// </remarks>
    public async ValueTask<Result> Run(
        string code,
        Sandbox? sandbox = null,
        CancellationToken ct = default)
    {
        await using var session = Session(sandbox);
        return await session.Run(code, ct);
    }

    /// <summary>
    /// Runs source text in a temporary session and converts the first Lua value to the requested type.
    /// </summary>
    /// <typeparam name="T">The expected C# result type.</typeparam>
    /// <param name="code">Lua source text encoded as UTF-16 in the host application.</param>
    /// <param name="sandbox">Sandbox policy for the temporary session, or <see cref="Sandbox.Untrusted"/> when omitted.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A typed result containing the converted first Lua return value, or a PopLua error.</returns>
    public async ValueTask<Result<T>> Run<T>(
        string code,
        Sandbox? sandbox = null,
        CancellationToken ct = default)
    {
        await using var session = Session(sandbox);
        return await session.Run<T>(code, ct);
    }

    /// <summary>
    /// Releases runtime resources. Current preview runtimes own no unmanaged state; sessions own Lua states.
    /// </summary>
    /// <returns>A completed task.</returns>
    /// <remarks>
    /// Disposing the runtime does not dispose sessions already created from it.
    /// Dispose each <see cref="Session"/> independently.
    /// </remarks>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

namespace PopLua.Context;

/// <summary>
/// Host-side execution context passed to generated Lua bindings.
/// </summary>
/// <remarks>
/// Bindings receive this value by declaring a first parameter annotated with
/// <see cref="ContextAttribute"/>. The context is valid for the current
/// execution and should not be stored for later script runs.
/// </remarks>
public sealed class ScriptContext
{
    internal ScriptContext(
        Identity identity,
        Sandbox sandbox,
        IServiceProvider services,
        CancellationToken cancellation,
        ExecutionState state)
    {
        Identity = identity;
        Sandbox = sandbox;
        Services = services;
        Cancellation = cancellation;
        State = state;
    }

    /// <summary>
    /// Gets the identity assigned to the current script execution.
    /// </summary>
    public Identity Identity { get; }

    /// <summary>
    /// Gets the sandbox policy active for the current script execution.
    /// </summary>
    public Sandbox Sandbox { get; }

    /// <summary>
    /// Gets the services available to generated bindings during this execution.
    /// </summary>
    /// <value>
    /// The session service provider when supplied; otherwise the runtime default
    /// provider or an empty provider.
    /// </value>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Gets the cancellation token that stops the current Lua execution and async bridge work.
    /// </summary>
    public CancellationToken Cancellation { get; }

    /// <summary>
    /// Gets live execution counters for diagnostics and generated callbacks.
    /// </summary>
    /// <remarks>
    /// This is live state, not a final metrics snapshot. Use
    /// <see cref="Metrics"/> received by <see cref="IDiagnostics.Completed"/>
    /// for completed executions.
    /// </remarks>
    public ExecutionState State { get; }

    internal static ScriptContext Create(
        Sandbox? sandbox = null,
        Identity? identity = null,
        IServiceProvider? services = null,
        CancellationToken cancellation = default)
        => new(
            identity ?? Identity.Anonymous,
            sandbox ?? Sandbox.Untrusted,
            services ?? PopLua.Context.Services.Empty,
            cancellation,
            new ExecutionState());
}

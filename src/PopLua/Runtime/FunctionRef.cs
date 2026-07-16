namespace PopLua.Runtime;

/// <summary>
/// Holds a Lua function alive for later calls while its owning session is alive.
/// </summary>
/// <remarks>
/// A function reference is owned by one <see cref="Session"/>. Dispose it to
/// release the Lua registry reference. It is not serializable and cannot be used
/// after the reference or its session is disposed. Calls use the owning session
/// and are rejected while that session is already executing Lua code.
/// </remarks>
public sealed class FunctionRef : IAsyncDisposable
{
    private readonly Session _session;
    private readonly int _registryReference;
    private bool _disposed;

    internal FunctionRef(Session session, int registryReference)
    {
        _session = session;
        _registryReference = registryReference;
    }

    /// <summary>
    /// Gets the session that owns this Lua function reference.
    /// </summary>
    /// <remarks>
    /// The reference may only be invoked while this session remains alive.
    /// </remarks>
    public Session Session => _session;

    internal int RegistryReference => _registryReference;

    /// <summary>
    /// Calls the referenced Lua function with the provided arguments.
    /// </summary>
    /// <param name="args">Arguments pushed to Lua before the call.</param>
    /// <returns>A result containing the first Lua return value, or a PopLua error.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the function reference or owning session is disposed.</exception>
    public ValueTask<Result> Call(params Value[] args)
        => Call(args, CancellationToken.None);

    /// <summary>
    /// Calls the referenced Lua function with the provided arguments.
    /// </summary>
    /// <param name="args">Arguments pushed to Lua before the call.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A result containing the first Lua return value, or a PopLua error.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the function reference or owning session is disposed.</exception>
    public ValueTask<Result> Call(Value[] args, CancellationToken ct)
    {
        ThrowIfDisposed();
        return _session.CallFunction(this, args, ct);
    }

    /// <summary>
    /// Calls the referenced Lua function and converts the first return value.
    /// </summary>
    /// <typeparam name="T">The expected C# result type.</typeparam>
    /// <param name="args">Arguments pushed to Lua before the call.</param>
    /// <returns>A typed result containing the converted first Lua return value, or a PopLua error.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the function reference or owning session is disposed.</exception>
    public ValueTask<Result<T>> Call<T>(params Value[] args)
        => Call<T>(args, CancellationToken.None);

    /// <summary>
    /// Calls the referenced Lua function and converts the first return value.
    /// </summary>
    /// <typeparam name="T">The expected C# result type.</typeparam>
    /// <param name="args">Arguments pushed to Lua before the call.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A typed result containing the converted first Lua return value, or a PopLua error.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the function reference or owning session is disposed.</exception>
    public async ValueTask<Result<T>> Call<T>(Value[] args, CancellationToken ct)
    {
        ThrowIfDisposed();
        var result = await _session.CallFunction(this, args, ct).ConfigureAwait(false);
        return Session.ToTypedResult<T>(result);
    }

    /// <summary>
    /// Releases the Lua registry reference.
    /// </summary>
    /// <returns>A completed task after the registry reference has been released.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the owning session is currently executing Lua code.</exception>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _session.ReleaseFunction(this);
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    internal void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FunctionRef));
    }
}

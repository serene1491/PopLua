namespace PopLua.Marshaling;

/// <summary>
/// Result of a Lua operation that returns a generic Lua value.
/// </summary>
/// <remarks>
/// Script, sandbox, quota, cancellation, type-conversion, and async task
/// failures are represented as failed results. Host misuse, such as using a
/// disposed session, may still throw directly.
/// </remarks>
public readonly struct Result
{
    private Result(bool ok, Value value, RuntimeException? error)
    {
        Ok = ok;
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Gets whether the Lua operation completed successfully.
    /// </summary>
    public bool Ok { get; }

    /// <summary>
    /// Gets the returned Lua value when <see cref="Ok"/> is true.
    /// </summary>
    /// <value>
    /// The first Lua return value on success, or <see cref="Value.Nil"/> on
    /// failure.
    /// </value>
    public Value Value { get; }

    /// <summary>
    /// Gets the PopLua error when <see cref="Ok"/> is false.
    /// </summary>
    /// <value>
    /// A <see cref="RuntimeException"/> on failure, or <see langword="null"/> on
    /// success.
    /// </value>
    public RuntimeException? Error { get; }

    /// <summary>
    /// Creates a successful Lua result.
    /// </summary>
    /// <param name="value">The first Lua return value.</param>
    /// <returns>A successful result.</returns>
    public static Result Success(Value value = default) => new(true, value, null);

    /// <summary>
    /// Creates a failed Lua result from a PopLua error.
    /// </summary>
    /// <param name="error">The error that stopped execution.</param>
    /// <returns>A failed result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static Result Failure(RuntimeException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, Value.Nil, error);
    }

    /// <summary>
    /// Throws the stored error when the result failed.
    /// </summary>
    /// <exception cref="RuntimeException">Thrown when <see cref="Ok"/> is <see langword="false"/>.</exception>
    public void ThrowIfError()
    {
        if (!Ok)
            throw Error ?? new ScriptException("Lua execution failed.");
    }
}

namespace PopLua.Marshaling;

/// <summary>
/// Result of a Lua operation that returns a typed value.
/// </summary>
/// <typeparam name="T">The requested C# result type.</typeparam>
/// <remarks>
/// Failed results keep the original <see cref="RuntimeException"/> in
/// <see cref="Error"/>. Use <see cref="Or"/> only when it is acceptable to hide
/// script, quota, sandbox, or cancellation failures behind a fallback value.
/// </remarks>
public readonly struct Result<T>
{
    private Result(bool ok, T? value, RuntimeException? error)
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
    /// Gets the typed result value when <see cref="Ok"/> is true.
    /// </summary>
    /// <value>
    /// The converted first Lua return value on success, or <see langword="null"/>
    /// / <see langword="default"/> on failure.
    /// </value>
    public T? Value { get; }

    /// <summary>
    /// Gets the PopLua error when <see cref="Ok"/> is false.
    /// </summary>
    /// <value>
    /// A <see cref="RuntimeException"/> on failure, or <see langword="null"/> on
    /// success.
    /// </value>
    public RuntimeException? Error { get; }

    /// <summary>
    /// Creates a successful typed Lua result.
    /// </summary>
    /// <param name="value">The typed result value.</param>
    /// <returns>A successful typed result.</returns>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed typed Lua result from a PopLua error.
    /// </summary>
    /// <param name="error">The error that stopped execution.</param>
    /// <returns>A failed typed result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
    public static Result<T> Failure(RuntimeException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, default, error);
    }

    /// <summary>
    /// Returns the value, or throws a script error when the result failed.
    /// </summary>
    /// <returns>The typed result value.</returns>
    /// <exception cref="RuntimeException">Thrown when <see cref="Ok"/> is <see langword="false"/>.</exception>
    public T Unwrap()
    {
        if (Ok)
            return Value!;

        throw Error ?? new ScriptException("Lua execution failed.");
    }

    /// <summary>
    /// Returns the value when successful, otherwise returns the provided fallback.
    /// </summary>
    /// <param name="fallback">Value returned when the result failed.</param>
    /// <returns>The successful value or <paramref name="fallback"/>.</returns>
    public T Or(T fallback) => Ok ? Value! : fallback;
}

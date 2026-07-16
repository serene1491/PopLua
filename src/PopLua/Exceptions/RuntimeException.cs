namespace PopLua.Exceptions;

/// <summary>
/// Base type for PopLua errors.
/// </summary>
/// <remarks>
/// Execution APIs return these errors through <see cref="Result.Error"/> or
/// <see cref="Result{T}.Error"/>. Host misuse, invalid arguments, and
/// disposed objects may still throw ordinary .NET exceptions directly.
/// </remarks>
public abstract class RuntimeException(string message, Exception? inner = null)
    : Exception(message, inner);

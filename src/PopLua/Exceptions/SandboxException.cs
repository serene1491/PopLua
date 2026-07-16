namespace PopLua.Exceptions;

/// <summary>
/// Represents a denied sandbox capability.
/// </summary>
/// <remarks>
/// This exception identifies the denied capability label. The host application
/// remains responsible for mapping capability labels to real file, network,
/// process, environment, or application-specific authorization checks.
/// </remarks>
public sealed class SandboxException(string cap)
    : RuntimeException($"Capability '{cap}' is not allowed.")
{
    /// <summary>
    /// Gets the denied capability name.
    /// </summary>
    public string Cap { get; } = cap;
}

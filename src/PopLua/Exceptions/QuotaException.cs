namespace PopLua.Exceptions;

/// <summary>
/// Represents a Lua quota violation.
/// </summary>
/// <remarks>
/// Quota violations are returned as failed execution results and also reported
/// through <see cref="IDiagnostics.QuotaBlocked"/>.
/// </remarks>
public sealed class QuotaException(QuotaKind kind)
    : RuntimeException($"Lua quota exceeded: {kind}.")
{
    /// <summary>
    /// Gets the quota that stopped execution.
    /// </summary>
    public QuotaKind Kind { get; } = kind;
}

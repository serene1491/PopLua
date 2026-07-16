namespace PopLua.Diagnostics;

/// <summary>
/// Receives Lua execution diagnostics.
/// </summary>
/// <remarks>
/// PopLua calls this interface synchronously from execution paths. Keep
/// implementations lightweight and store or forward events in host-owned
/// telemetry systems. PopLua does not aggregate diagnostics for you.
/// </remarks>
public interface IDiagnostics
{
    /// <summary>
    /// Called when a Lua chunk or function execution starts.
    /// </summary>
    /// <param name="ctx">Execution context, including identity and sandbox.</param>
    /// <param name="chunk">Chunk being executed. Global calls use a synthetic <c>call:name</c> chunk.</param>
    void Started(ScriptContext ctx, Chunk chunk);

    /// <summary>
    /// Called after Lua execution completes successfully.
    /// </summary>
    /// <param name="ctx">Execution context, including identity and sandbox.</param>
    /// <param name="metrics">Final metrics for the completed execution.</param>
    /// <remarks>
    /// This callback does not include the chunk directly. Correlate it with the
    /// preceding <see cref="Started"/> callback using the identity and your own
    /// execution bookkeeping.
    /// </remarks>
    void Completed(ScriptContext ctx, in Metrics metrics);

    /// <summary>
    /// Called after Lua execution fails with a PopLua error.
    /// </summary>
    /// <param name="ctx">Execution context, including identity and sandbox.</param>
    /// <param name="error">Error returned to the host in the failed <see cref="Result"/>.</param>
    void Failed(ScriptContext ctx, RuntimeException error);

    /// <summary>
    /// Called when a configured resource quota stops execution.
    /// </summary>
    /// <param name="ctx">Execution context, including identity and sandbox.</param>
    /// <param name="kind">Quota that stopped execution.</param>
    /// <remarks>
    /// This is followed by <see cref="Failed"/> with a <see cref="QuotaException"/>.
    /// </remarks>
    void QuotaBlocked(ScriptContext ctx, QuotaKind kind);

    /// <summary>
    /// Called when a denied sandbox capability blocks module registration or use.
    /// </summary>
    /// <param name="ctx">Execution context, including identity and sandbox.</param>
    /// <param name="cap">Denied capability name.</param>
    /// <remarks>
    /// Capability checks are host-defined. This callback identifies the denied
    /// label; it does not describe an operating-system permission decision.
    /// </remarks>
    void SandboxBlocked(ScriptContext ctx, string cap);
}

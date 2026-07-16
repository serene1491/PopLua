namespace PopLua.Diagnostics;

/// <summary>
/// Observes logical runs, execution steps, and controlled module loading.
/// </summary>
/// <remarks>
/// All callbacks are synchronous, optional, and isolated from Lua execution.
/// Implement only the events the host needs and return quickly.
/// </remarks>
public interface IRunObserver
{
    /// <summary>
    /// Called when an execution step starts.
    /// </summary>
    /// <param name="ctx">Current execution context.</param>
    /// <param name="step">Step that started.</param>
    void Started(ScriptContext ctx, Step step) { }

    /// <summary>
    /// Called when an execution step completes successfully.
    /// </summary>
    /// <param name="ctx">Current execution context.</param>
    /// <param name="step">Step that completed.</param>
    /// <param name="metrics">Final execution metrics.</param>
    void Completed(ScriptContext ctx, Step step, in Metrics metrics) { }

    /// <summary>
    /// Called when an execution step fails.
    /// </summary>
    /// <param name="ctx">Current execution context.</param>
    /// <param name="step">Step that failed.</param>
    /// <param name="error">Error returned to the host.</param>
    void Failed(ScriptContext ctx, Step step, RuntimeException error) { }

    /// <summary>
    /// Called when a quota stops an execution step.
    /// </summary>
    /// <param name="ctx">Current execution context.</param>
    /// <param name="step">Blocked step.</param>
    /// <param name="kind">Quota that stopped execution.</param>
    void Quota(ScriptContext ctx, Step step, QuotaKind kind) { }

    /// <summary>
    /// Called when a capability check denies an execution step.
    /// </summary>
    /// <param name="ctx">Current execution context.</param>
    /// <param name="step">Blocked step.</param>
    /// <param name="cap">Denied host capability.</param>
    void Denied(ScriptContext ctx, Step step, string cap) { }

    /// <summary>
    /// Called before a validated controlled-loading request is resolved.
    /// </summary>
    /// <param name="ctx">Current execution context.</param>
    /// <param name="load">Validated load request.</param>
    void Loading(ScriptContext ctx, Load load) { }

    /// <summary>
    /// Called after a controlled module is loaded or read from the session cache.
    /// </summary>
    /// <param name="ctx">Current execution context.</param>
    /// <param name="load">Completed load request.</param>
    /// <param name="cached">Whether the module value came from the session cache.</param>
    void Loaded(ScriptContext ctx, Load load, bool cached) { }

    /// <summary>
    /// Called when a validated controlled-loading request fails.
    /// </summary>
    /// <param name="ctx">Current execution context.</param>
    /// <param name="load">Failed load request.</param>
    /// <param name="error">Load error exposed through the Lua execution.</param>
    void LoadFailed(ScriptContext ctx, Load load, RuntimeException error) { }
}

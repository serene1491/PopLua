namespace PopLua.Context;

/// <summary>
/// Runtime counters for a Lua execution.
/// </summary>
/// <remarks>
/// Values are updated while Lua is running and are intended for generated
/// callbacks and diagnostics. They are not an aggregate telemetry store.
/// </remarks>
public sealed class ExecutionState
{
    internal ExecutionState()
    {
    }

    /// <summary>
    /// Gets the approximate number of Lua VM instructions observed by quota hooks.
    /// </summary>
    public long Instructions { get; internal set; }

    /// <summary>
    /// Gets the peak Lua allocator usage, in bytes, for the owning session.
    /// </summary>
    /// <remarks>
    /// This measures Lua allocator memory only, not managed allocations made by
    /// host callbacks or service objects.
    /// </remarks>
    public long PeakMemoryBytes { get; internal set; }

    /// <summary>
    /// Gets the current Lua call depth observed by call/return hooks.
    /// </summary>
    /// <remarks>
    /// For final maximum depth, use <see cref="Metrics.MaxCallDepth"/> from
    /// diagnostics.
    /// </remarks>
    public int CallDepth { get; internal set; }

    /// <summary>
    /// Gets the elapsed wall-clock execution time observed so far.
    /// </summary>
    /// <remarks>
    /// This includes time spent suspended on async operations. Lua active-time quotas
    /// use active Lua execution time instead.
    /// </remarks>
    public TimeSpan Elapsed { get; internal set; }
}

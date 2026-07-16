namespace PopLua.Diagnostics;

/// <summary>
/// Snapshot of a completed Lua execution.
/// </summary>
/// <param name="Duration">Total wall-clock duration for the execution, including time suspended on async operations.</param>
/// <param name="Instructions">Approximate Lua VM instructions observed by quota hooks.</param>
/// <param name="PeakMemoryBytes">Peak Lua allocator usage in bytes, excluding managed host allocations.</param>
/// <param name="MaxCallDepth">Maximum Lua call depth observed during execution.</param>
/// <remarks>
/// Metrics are a final per-execution snapshot. Active-time quota enforcement uses
/// active Lua execution time, while <see cref="Duration"/> is wall-clock time.
/// </remarks>
public readonly record struct Metrics(
    TimeSpan Duration,
    long Instructions,
    long PeakMemoryBytes,
    int MaxCallDepth);

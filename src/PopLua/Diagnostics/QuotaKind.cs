namespace PopLua.Diagnostics;

/// <summary>
/// Identifies the quota that stopped Lua execution.
/// </summary>
public enum QuotaKind
{
    /// <summary>
    /// Lua execution exceeded the configured instruction budget.
    /// </summary>
    Instructions,

    /// <summary>
    /// Lua execution exceeded the configured active execution time budget.
    /// </summary>
    ActiveTime,

    /// <summary>
    /// Lua execution exceeded the configured total wall-clock lifetime budget.
    /// </summary>
    WallTime,

    /// <summary>
    /// Lua allocator usage exceeded the configured heap limit.
    /// </summary>
    Memory,

    /// <summary>
    /// Lua execution exceeded the configured call-depth limit.
    /// </summary>
    CallDepth,
}

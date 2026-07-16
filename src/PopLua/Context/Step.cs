namespace PopLua.Context;

/// <summary>
/// Describes one observable operation inside a logical run.
/// </summary>
/// <param name="Id">Run-local step identifier.</param>
/// <param name="Kind">Operation kind.</param>
/// <param name="Name">Chunk, function, or normalized module name.</param>
/// <param name="Parent">Optional parent step identifier.</param>
public readonly record struct Step(string Id, StepKind Kind, string Name, string? Parent = null);

/// <summary>
/// Identifies the operation represented by a <see cref="Step"/>.
/// </summary>
public enum StepKind
{
    /// <summary>
    /// A Lua chunk execution.
    /// </summary>
    Run,

    /// <summary>
    /// A Lua function call.
    /// </summary>
    Call,

    /// <summary>
    /// A controlled module load.
    /// </summary>
    Load,
}

namespace PopLua.Exceptions;

/// <summary>
/// Represents a Lua syntax error, runtime error, or explicit error() call.
/// </summary>
/// <remarks>
/// Compile errors usually include <see cref="Chunk"/> and <see cref="Line"/> but
/// no runtime traceback. Runtime errors include Lua's traceback when it can be
/// captured safely.
/// </remarks>
public sealed class ScriptException(string message, string? trace = null, Exception? inner = null)
    : RuntimeException(message, inner)
{
    /// <summary>
    /// Gets the Lua traceback when one is available.
    /// </summary>
    /// <value>
    /// Lua's real traceback text, or <see langword="null"/> for compile errors,
    /// cancellation, managed callback failures, or cases where Lua cannot
    /// produce a traceback.
    /// </value>
    public string? LuaTrace { get; } = trace;

    /// <summary>
    /// Gets the chunk name associated with the error when known.
    /// </summary>
    /// <value>
    /// The parsed Lua chunk name or the host-provided chunk fallback.
    /// </value>
    public string? Chunk { get; init; }

    /// <summary>
    /// Gets the Lua source line associated with the error when known.
    /// </summary>
    /// <value>
    /// The parsed Lua source line, or <see langword="null"/> when the location
    /// is unavailable.
    /// </value>
    public int? Line { get; init; }
}

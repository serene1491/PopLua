namespace PopLua.Marshaling;

/// <summary>
/// Describes the Lua kind stored by a <see cref="Value"/>.
/// </summary>
public enum ValueKind : byte
{
    /// <summary>
    /// Lua nil.
    /// </summary>
    Nil,

    /// <summary>
    /// Lua boolean.
    /// </summary>
    Bool,

    /// <summary>
    /// Lua integer.
    /// </summary>
    Int,

    /// <summary>
    /// Lua floating-point number.
    /// </summary>
    Number,

    /// <summary>
    /// Lua string.
    /// </summary>
    String,

    /// <summary>
    /// Lua table value observed as an opaque kind.
    /// </summary>
    /// <remarks>
    /// PopLua does not expose public table references for this kind in `1.0`.
    /// Use generated descriptor parameters for structured table input.
    /// </remarks>
    Table,

    /// <summary>
    /// Lua function value observed as an opaque kind.
    /// </summary>
    /// <remarks>
    /// Use <see cref="FunctionRef"/> parameters when generated bindings need
    /// to retain and call a session-owned Lua callback.
    /// </remarks>
    Function,

    /// <summary>
    /// Lua full userdata value observed as an opaque kind.
    /// </summary>
    /// <remarks>
    /// Use typed generated userdata parameters and return values when C# needs
    /// to receive or return host objects. Userdata inside `Value[]` varargs
    /// is intentionally opaque.
    /// </remarks>
    Userdata,
}

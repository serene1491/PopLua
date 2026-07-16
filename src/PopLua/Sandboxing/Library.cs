namespace PopLua.Sandboxing;

/// <summary>
/// Lua 5.4 or 5.5 native standard libraries that a sandbox may expose.
/// </summary>
/// <remarks>
/// <see cref="Sandbox.Untrusted"/> exposes none of these by default.
/// <see cref="Sandbox.Trusted"/> exposes <see cref="All"/> and should be used
/// only for trusted host-owned scripts. For user-authored scripts, prefer
/// <see cref="Safe"/> or an explicit smaller set.
/// </remarks>
[Flags]
public enum Library
{
    /// <summary>
    /// No native Lua standard libraries.
    /// </summary>
    None = 0,

    /// <summary>
    /// Selected base functions: <c>assert</c>, <c>error</c>, <c>ipairs</c>,
    /// <c>pairs</c>, <c>pcall</c>, <c>select</c>, <c>tonumber</c>,
    /// <c>tostring</c>, and <c>type</c>.
    /// </summary>
    /// <remarks>
    /// PopLua opens Lua's native base library and removes unsafe or broad
    /// globals such as <c>dofile</c>, <c>load</c>, <c>loadfile</c>,
    /// <c>collectgarbage</c>, <c>print</c>, and raw/metatable helpers.
    /// </remarks>
    SafeBase = 1 << 0,

    /// <summary>
    /// Lua's native <c>coroutine</c> library.
    /// </summary>
    Coroutine = 1 << 1,

    /// <summary>
    /// Lua's native <c>math</c> library.
    /// </summary>
    Math = 1 << 2,

    /// <summary>
    /// Lua's native <c>string</c> library.
    /// </summary>
    String = 1 << 3,

    /// <summary>
    /// Lua's native <c>table</c> library.
    /// </summary>
    Table = 1 << 4,

    /// <summary>
    /// Lua's native <c>utf8</c> library.
    /// </summary>
    Utf8 = 1 << 5,

    /// <summary>
    /// Lua's full native base library.
    /// </summary>
    /// <remarks>
    /// This includes broad helpers such as <c>load</c>, <c>dofile</c>,
    /// <c>loadfile</c>, <c>collectgarbage</c>, <c>print</c>, and raw/metatable
    /// helpers. Avoid this for untrusted user-authored scripts.
    /// </remarks>
    FullBase = 1 << 6,

    /// <summary>
    /// Lua's native <c>package</c> library, including package search behavior.
    /// </summary>
    /// <remarks>
    /// This is separate from PopLua's controlled <c>require</c> and should not
    /// be used for untrusted scripts unless the host deliberately accepts Lua's
    /// package loading behavior.
    /// </remarks>
    Package = 1 << 7,

    /// <summary>
    /// Lua's native <c>io</c> library.
    /// </summary>
    Io = 1 << 8,

    /// <summary>
    /// Lua's native <c>os</c> library.
    /// </summary>
    Os = 1 << 9,

    /// <summary>
    /// Lua's native <c>debug</c> library.
    /// </summary>
    Debug = 1 << 10,

    /// <summary>
    /// Conservative native libraries suitable for many user-authored scripts.
    /// </summary>
    Safe = SafeBase | Math | String | Table | Utf8,

    /// <summary>
    /// All native Lua 5.4 or 5.5 standard libraries.
    /// </summary>
    /// <remarks>
    /// Equivalent to the standard Lua library set opened by
    /// <see cref="Sandbox.Trusted"/>.
    /// </remarks>
    All = FullBase | Coroutine | Math | String | Table | Utf8 | Package | Io | Os | Debug,
}

namespace PopLua.Binding;

/// <summary>
/// Marks a method as a Lua function.
/// </summary>
/// <remarks>
/// The method must be public. When <paramref name="name"/> is omitted, the
/// generator exposes the C# method name converted to snake_case. Methods that
/// return <see cref="ValueTask"/> or <see cref="ValueTask{TResult}"/> use the
/// async coroutine bridge automatically. <see cref="Task"/> and
/// <see cref="Task{TResult}"/> are intentionally unsupported binding shapes.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FnAttribute(string? name = null) : Attribute
{
    /// <summary>
    /// Gets the Lua-visible function name, or <see langword="null"/> to use the C# method name.
    /// </summary>
    /// <value>
    /// The exact Lua function name when supplied; otherwise the generator uses
    /// the C# method name converted to snake_case.
    /// </value>
    public string? Name { get; } = name;

    /// <summary>
    /// Gets or sets the capability required before the generated function is called.
    /// </summary>
    /// <remarks>
    /// Function capabilities complement module capabilities for modules that
    /// mix read-only and privileged operations. The capability is emitted in
    /// generated manifests and checked before host code runs.
    /// </remarks>
    public string? Cap { get; init; }

    /// <summary>
    /// Gets or sets whether suspended async time counts against the active-time quota.
    /// </summary>
    /// <remarks>
    /// Async waits do not count by default. Set this only for host work whose
    /// waiting time is intentionally billable. Wall-time always continues.
    /// </remarks>
    public bool CountWait { get; init; }
}

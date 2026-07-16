namespace PopLua.Binding;

/// <summary>
/// Marks a method as a Lua function.
/// </summary>
/// <remarks>
/// The method must be public. When <paramref name="name"/> is omitted, the
/// generator exposes the C# method name converted to snake_case. Async module
/// functions and userdata instance methods are supported when
/// <see cref="Async"/> is set and the method returns <see cref="ValueTask"/>
/// or <see cref="ValueTask{TResult}"/>.
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
    /// Gets or sets whether the generated function uses the async coroutine bridge.
    /// </summary>
    /// <remarks>
    /// Async functions must return <see cref="ValueTask"/> or
    /// <see cref="ValueTask{TResult}"/>. <see cref="Task"/> and
    /// <see cref="Task{TResult}"/> are intentionally unsupported by generated
    /// bindings. Cancellation is terminal for the active execution; async
    /// operation faults are raised as Lua errors by the coroutine wrapper and
    /// can be caught by Lua <c>pcall</c> when the sandbox exposes it.
    /// </remarks>
    public bool Async { get; init; }

    /// <summary>
    /// Gets or sets whether suspended async time pauses the active-time quota.
    /// </summary>
    /// <remarks>
    /// This option applies only when <see cref="Async"/> is <see langword="true"/>.
    /// Work performed before the first suspension and after resumption still
    /// counts as active time. Wall-time quota always continues to run.
    /// </remarks>
    public bool PauseTime { get; init; }
}

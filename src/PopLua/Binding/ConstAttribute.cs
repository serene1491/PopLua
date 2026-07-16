namespace PopLua.Binding;

/// <summary>
/// Marks a field or property as a Lua constant.
/// </summary>
/// <remarks>
/// Constants are emitted into a generated module table when the session
/// registers modules. They are Lua values, not live views over the C# field or
/// property after registration.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ConstAttribute(string? name = null) : Attribute
{
    /// <summary>
    /// Gets the Lua-visible name, or <see langword="null"/> to use the C# member name.
    /// </summary>
    public string? Name { get; } = name;
}

namespace PopLua.Binding;

/// <summary>
/// Marks a field, property, or computed module property method as visible to Lua.
/// </summary>
/// <remarks>
/// Static fields and properties on modules are registered as module values.
/// Module methods marked with this attribute are generated as computed
/// properties and may accept an optional first <see cref="ContextAttribute"/>
/// parameter. Userdata properties are exposed through the userdata metatable
/// and can be made writable when the userdata type enables setters.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method)]
public sealed class PropAttribute(string? name = null) : Attribute
{
    /// <summary>
    /// Gets the Lua-visible property name, or <see langword="null"/> to use the C# member name.
    /// </summary>
    public string? Name { get; } = name;

    /// <summary>
    /// Gets or sets whether generated userdata exposes only a getter for this property.
    /// </summary>
    /// <remarks>
    /// Module values are registered when the session is created. This flag is
    /// meaningful for userdata properties.
    /// </remarks>
    public bool ReadOnly { get; init; }
}

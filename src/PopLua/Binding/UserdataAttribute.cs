namespace PopLua.Binding;

/// <summary>
/// Marks a class or struct as a Lua userdata type.
/// </summary>
/// <remarks>
/// Userdata types must be `partial`. Instances returned from generated bindings
/// are wrapped in Lua userdata with a managed handle that is released by the
/// generated `__gc` finalizer when enabled. Allocation-heavy userdata loops are
/// currently more expensive than primitive module calls.
/// </remarks>
/// <example>
/// <code>
/// [Userdata("vec2")]
/// public partial class Vec2
/// {
///     [Prop("x", ReadOnly = true)]
///     public double X { get; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class UserdataAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the Lua userdata type name used for the generated metatable.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets or sets whether generated userdata properties may be assigned from Lua.
    /// </summary>
    /// <remarks>
    /// Setters are generated only for writable fields and properties that the
    /// generator can marshal. Mutating struct userdata updates the boxed managed
    /// instance stored in Lua userdata, not an external copy held elsewhere.
    /// </remarks>
    public bool Setters { get; init; }

    /// <summary>
    /// Gets or sets whether generated userdata maps <see cref="object.ToString"/> to Lua <c>tostring</c>.
    /// </summary>
    /// <remarks>
    /// Disable this when the host type's <see cref="object.ToString"/> output is
    /// not suitable for script authors.
    /// </remarks>
    public new bool ToString { get; init; } = true;

    /// <summary>
    /// Gets or sets whether generated userdata installs a Lua finalizer to release the managed handle.
    /// </summary>
    /// <remarks>
    /// Disabling this is an advanced option for generated binding support. Hosts
    /// should normally keep it enabled so managed handles are released when Lua
    /// collects the userdata.
    /// </remarks>
    public bool Gc { get; init; } = true;
}

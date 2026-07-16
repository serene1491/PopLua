namespace PopLua.Binding;

/// <summary>
/// Marks a class or struct as a Lua module.
/// </summary>
/// <remarks>
/// Module types must be `partial` so the source generator can emit registration
/// code. Public methods marked with <see cref="FnAttribute"/> become functions
/// on the Lua global module table.
/// </remarks>
/// <example>
/// <code>
/// [Module("mathx")]
/// public partial class MathModule
/// {
///     [Fn("add")]
///     public static long Add(long left, long right) => left + right;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ModuleAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the Lua global table name used for generated module members.
    /// </summary>
    /// <value>
    /// The table name visible to Lua, such as <c>mathx</c> in
    /// <c>mathx.add(1, 2)</c>.
    /// </value>
    public string Name { get; } = name;

    /// <summary>
    /// Gets or sets the sandbox capability required before the module is registered.
    /// </summary>
    /// <remarks>
    /// The capability is checked during session module registration. If the
    /// session sandbox does not allow it, the whole module table is hidden.
    /// </remarks>
    public string? Cap { get; init; }
}

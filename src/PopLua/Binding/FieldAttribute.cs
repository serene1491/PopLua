namespace PopLua.Binding;

/// <summary>
/// Sets the Lua-visible name of one generated descriptor field.
/// </summary>
/// <remarks>
/// Without this attribute, descriptor fields use the C# property name converted
/// to snake_case.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FieldAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the exact Lua table field name.
    /// </summary>
    public string Name { get; } = name;
}

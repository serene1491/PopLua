namespace PopLua.Binding;

/// <summary>
/// Marks a copy-based DTO that generated bindings may return as a Lua table.
/// </summary>
/// <remarks>
/// Public readable properties become fields in a fresh Lua table. Apply
/// <see cref="IgnoreAttribute"/> to properties that must not be exposed.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TableAttribute : Attribute;

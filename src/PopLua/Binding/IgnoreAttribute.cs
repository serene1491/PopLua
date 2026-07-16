namespace PopLua.Binding;

/// <summary>
/// Excludes a member from generated Lua bindings.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IgnoreAttribute : Attribute;

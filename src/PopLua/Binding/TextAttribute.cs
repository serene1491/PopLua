namespace PopLua.Binding;

/// <summary>
/// Marks a Lua-facing <see cref="string"/> parameter as display text.
/// </summary>
///
/// <remarks>
/// Text parameters accept Lua strings, scalar values, and values whose
/// metatable defines <c>__tostring</c>. Unmarked string parameters remain
/// strict, which keeps identifiers, keys, and protocol values type-safe.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TextAttribute : Attribute;

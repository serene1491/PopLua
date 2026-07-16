namespace PopLua.Binding;

/// <summary>
/// Marks a parameter as the injected Lua context.
/// </summary>
/// <remarks>
/// The annotated parameter must be the first parameter of a generated function.
/// It is supplied by PopLua and is not visible as a Lua argument.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ContextAttribute : Attribute;

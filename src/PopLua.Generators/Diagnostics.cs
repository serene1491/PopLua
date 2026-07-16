using Microsoft.CodeAnalysis;

namespace PopLua.Generators;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor NonPublicFunction = new(
        "PLUA001",
        "Lua function must be public",
        "[Fn] method '{0}' must be public",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedType = new(
        "PLUA002",
        "Unsupported Lua marshaling type",
        "Type '{0}' is not supported by the PopLua marshaler",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ModuleMustBePartial = new(
        "PLUA003",
        "Lua module must be partial",
        "[Module] type '{0}' must be partial",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ContextMustBeFirst = new(
        "PLUA004",
        "ScriptContext parameter must be first",
        "[Context] parameter on '{0}' must be the first parameter",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AsyncReturnType = new(
        "PLUA005",
        "Async Lua function has invalid return type",
        "[Fn(Async = true)] method '{0}' must return ValueTask or ValueTask<T>",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UserdataMustBePartial = new(
        "PLUA006",
        "Lua userdata must be partial",
        "[Userdata] type '{0}' must be partial",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor VariadicMustBeLast = new(
        "PLUA007",
        "Value array must be last",
        "Value[] parameter on '{0}' must be the last parameter",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateLuaName = new(
        "PLUA008",
        "Duplicate Lua name",
        "Lua name '{0}' is used more than once in module '{1}'",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsafeBlocksRequired = new(
        "PLUA010",
        "Generated Lua bindings require unsafe blocks",
        "Project '{0}' declares PopLua generated bindings and must enable <AllowUnsafeBlocks>true</AllowUnsafeBlocks>",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UserdataSelfParameter = new(
        "PLUA011",
        "Userdata receiver is supplied by PopLua",
        "[Fn] userdata method '{0}' must not declare a Value self parameter; use the C# instance as the receiver",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PauseTimeRequiresAsync = new(
        "PLUA012",
        "PauseTime requires an async Lua function",
        "[Fn] method '{0}' sets PauseTime but is not marked Async = true",
        "PopLua",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

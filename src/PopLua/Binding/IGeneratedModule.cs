using System.ComponentModel;

namespace PopLua.Binding;

/// <summary>
/// Contract implemented by source-generated Lua modules.
/// </summary>
/// <remarks>
/// This type is public so generated bindings in consumer assemblies can expose
/// static registration callbacks to PopLua. Application code should use
/// <see cref="EngineBuilder.Module{T}"/> or the generic
/// <c>EngineBuilder.Modules&lt;T1, T2&gt;(...)</c> overloads instead of
/// implementing it manually.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IGeneratedModule
{
    /// <summary>
    /// Gets the Lua global table name registered by the generated module.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Gets the sandbox capability required to expose the module, or <see langword="null"/> when no capability is required.
    /// </summary>
    static abstract string? Cap { get; }

    /// <summary>
    /// Registers generated functions, constants, and userdata metatables in a Lua session.
    /// </summary>
    static abstract void Register(Registration registration);
}

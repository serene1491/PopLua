using System.ComponentModel;

namespace PopLua.Binding;

/// <summary>
/// Runtime support registry populated by PopLua's source generator.
/// </summary>
/// <remarks>
/// This type is public so generated code in consumer assemblies can register
/// module descriptors without runtime assembly scanning. Application code should
/// use <see cref="EngineBuilder.Module{T}"/> or the generic
/// <c>EngineBuilder.Modules&lt;T1, T2&gt;(...)</c> overloads instead.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedModuleRegistry
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<Type, ModuleDescriptor> Modules = [];

    /// <summary>
    /// Registers a generated module descriptor for the source module type.
    /// </summary>
    /// <param name="moduleType">Source type marked with <see cref="ModuleAttribute"/>.</param>
    /// <param name="name">Lua-facing module name.</param>
    /// <param name="cap">Required sandbox capability, or <see langword="null"/>.</param>
    /// <param name="register">Generated registration callback.</param>
    /// <exception cref="ArgumentNullException">Thrown when required arguments are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
    public static void Register(
        Type moduleType,
        string name,
        string? cap,
        Action<Registration> register)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(register);

        lock (Gate)
            Modules[moduleType] = new ModuleDescriptor(name, cap, register);
    }

    internal static ModuleDescriptor Get<T>()
    {
        var moduleType = typeof(T);

        lock (Gate)
        {
            if (Modules.TryGetValue(moduleType, out var descriptor))
                return descriptor;
        }

        throw new InvalidOperationException(
            $"Type '{moduleType.FullName}' is not a generated PopLua module. " +
            "Add [Module] to a partial type in an unsafe-enabled generated binding project.");
    }
}

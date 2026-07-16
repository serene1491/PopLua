namespace PopLua.Binding;

/// <summary>
/// Collects generated Lua modules for a runtime.
/// </summary>
/// <remarks>
/// This collection is configured while building a <see cref="Engine"/>.
/// Generated modules are registered into each new session if the session
/// sandbox allows their required capability.
/// </remarks>
public sealed class ModuleCollection
{
    private readonly List<ModuleDescriptor> _modules = [];

    internal IReadOnlyList<ModuleDescriptor> Modules => _modules;

    /// <summary>
    /// Adds a source-generated Lua module to runtimes built by the current builder.
    /// </summary>
    /// <typeparam name="T">Source type marked with <see cref="ModuleAttribute"/>.</typeparam>
    /// <remarks>
    /// Runtime builders can also use the generic
    /// <c>EngineBuilder.Modules&lt;T1, T2&gt;(...)</c> overloads for compact
    /// source-visible registration.
    /// </remarks>
    public void Add<T>()
        => _modules.Add(GeneratedModuleRegistry.Get<T>());

    /// <summary>
    /// Removes all generated modules registered with the specified Lua module name.
    /// </summary>
    /// <param name="name">Lua-facing module name to remove.</param>
    /// <remarks>
    /// The name is compared using ordinal string comparison and matches the
    /// `[Module("name")]` value, not the C# type name.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public void Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _modules.RemoveAll(module => string.Equals(module.Name, name, StringComparison.Ordinal));
    }
}

using System.ComponentModel;

namespace PopLua.Binding;

/// <summary>
/// Runtime support registry populated by PopLua's source generator.
/// </summary>
/// <remarks>
/// This type is public so generated code in consumer assemblies can register
/// userdata descriptors without runtime assembly scanning. Application code
/// should expose userdata through generated module functions and properties.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedUserdataRegistry
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<Type, UserdataDescriptor> Userdata = [];

    /// <summary>
    /// Registers a generated userdata descriptor for the source userdata type.
    /// </summary>
    /// <param name="userdataType">Source type marked with <see cref="UserdataAttribute"/>.</param>
    /// <param name="metatableName">Generated Lua metatable name.</param>
    /// <param name="register">Generated metatable registration callback.</param>
    /// <exception cref="ArgumentNullException">Thrown when required arguments are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="metatableName"/> is empty or whitespace.</exception>
    public static void Register(
        Type userdataType,
        string metatableName,
        Action<Registration> register)
    {
        ArgumentNullException.ThrowIfNull(userdataType);
        ArgumentException.ThrowIfNullOrWhiteSpace(metatableName);
        ArgumentNullException.ThrowIfNull(register);

        lock (Gate)
            Userdata[userdataType] = new UserdataDescriptor(metatableName, register);
    }

    internal static UserdataDescriptor Get<T>()
    {
        var userdataType = typeof(T);

        lock (Gate)
        {
            if (Userdata.TryGetValue(userdataType, out var descriptor))
                return descriptor;
        }

        throw new InvalidOperationException(
            $"Type '{userdataType.FullName}' is not generated PopLua userdata. " +
            "Add [Userdata] to a partial type in an unsafe-enabled generated binding project.");
    }
}

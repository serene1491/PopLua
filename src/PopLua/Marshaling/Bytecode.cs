namespace PopLua.Marshaling;

/// <summary>
/// Precompiled Lua bytecode.
/// </summary>
/// <remarks>
/// Instances are produced by <see cref="Session.Compile(Chunk)"/> and own
/// their byte array. Bytecode is reusable across sessions in the same host
/// process, but it is Lua-version/runtime-specific and should not be accepted
/// from untrusted users.
/// </remarks>
public sealed class Bytecode
{
    internal Bytecode(byte[] data, string? name)
    {
        Data = data;
        Name = name;
    }

    /// <summary>
    /// Gets the compiled Lua bytecode bytes owned by this value.
    /// </summary>
    /// <value>
    /// Immutable bytecode data suitable for passing back to
    /// <see cref="Session.Run(Bytecode, System.Threading.CancellationToken)"/>.
    /// </value>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// Gets the chunk name associated with the compiled bytecode when one was provided.
    /// </summary>
    /// <value>
    /// The original chunk name used during compilation, or <see langword="null"/>.
    /// </value>
    public string? Name { get; }
}

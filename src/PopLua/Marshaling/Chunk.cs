using System.Text;

namespace PopLua.Marshaling;

/// <summary>
/// Lua source input.
/// </summary>
/// <remarks>
/// A chunk owns or references UTF-8 source bytes plus an optional diagnostic
/// name. Use stable names for user-authored scripts so compile errors, runtime
/// errors, tracebacks, diagnostics, and bytecode identify the original script.
/// </remarks>
public readonly record struct Chunk
{
    private Chunk(ReadOnlyMemory<byte> code, string? name)
    {
        Bytes = code;
        Name = name;
    }

    /// <summary>
    /// Gets the UTF-8 Lua source bytes for this chunk.
    /// </summary>
    /// <value>
    /// The memory provided to <see cref="Utf8"/> or allocated by
    /// <see cref="Code"/> / <see cref="File"/>.
    /// </value>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>
    /// Gets the optional chunk name used by Lua diagnostics and error messages.
    /// </summary>
    /// <value>
    /// A host-defined name such as <c>plugin:on_start.lua</c>, or
    /// <see langword="null"/> for anonymous chunks.
    /// </value>
    public string? Name { get; }

    /// <summary>
    /// Creates a Lua chunk from source text encoded as UTF-8.
    /// </summary>
    /// <param name="code">Lua source text.</param>
    /// <param name="name">Optional chunk name used by Lua diagnostics and bytecode.</param>
    /// <returns>A chunk containing a newly allocated UTF-8 copy of <paramref name="code"/>.</returns>
    /// <example>
    /// <code>
    /// var chunk = Chunk.Code(scriptText, name: "plugin:on_start.lua");
    /// </code>
    /// </example>
    public static Chunk Code(string code, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        return new Chunk(Encoding.UTF8.GetBytes(code), name);
    }

    /// <summary>
    /// Creates a Lua chunk from already encoded UTF-8 source bytes.
    /// </summary>
    /// <param name="code">UTF-8 Lua source bytes.</param>
    /// <param name="name">Optional chunk name used by Lua diagnostics and bytecode.</param>
    /// <returns>A chunk that references the supplied memory.</returns>
    /// <remarks>
    /// The supplied memory is not copied. Keep it valid and unchanged until the
    /// chunk has been compiled or run.
    /// </remarks>
    public static Chunk Utf8(ReadOnlyMemory<byte> code, string? name = null)
        => new(code, name);

    /// <summary>
    /// Reads a Lua source file and uses its path as the chunk name.
    /// </summary>
    /// <param name="path">Host file-system path to read.</param>
    /// <returns>A chunk containing the file bytes and using <paramref name="path"/> as the chunk name.</returns>
    /// <remarks>
    /// This is host-side file access and is not controlled by PopLua's Lua
    /// sandbox. Use it only for files the host has already decided to trust.
    /// The file is read as bytes and is expected to contain UTF-8 Lua source.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="System.IO.IOException">Thrown when the host cannot read the file.</exception>
    public static Chunk File(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return new Chunk(System.IO.File.ReadAllBytes(path), path);
    }
}

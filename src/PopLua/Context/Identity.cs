namespace PopLua.Context;

/// <summary>
/// Identifies the host-side entity running a Lua script.
/// </summary>
public sealed class Identity
{
    private Identity(string id, string? name, IReadOnlyDictionary<string, object> tags)
    {
        Id = id;
        Name = name;
        Tags = tags;
    }

    /// <summary>
    /// Gets the stable host-defined identity id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the optional display name for diagnostics and host policy decisions.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets immutable host-defined metadata associated with the identity.
    /// </summary>
    public IReadOnlyDictionary<string, object> Tags { get; }

    /// <summary>
    /// Gets the default identity used when a host does not provide one.
    /// </summary>
    public static Identity Anonymous { get; } = new("anonymous", null, EmptyTags.Instance);

    /// <summary>
    /// Gets an identity intended for trusted host-owned scripts.
    /// </summary>
    public static Identity System { get; } = new("system", "System", EmptyTags.Instance);

    /// <summary>
    /// Creates an immutable script identity for diagnostics, sandbox decisions, and generated callbacks.
    /// </summary>
    /// <param name="id">Stable host-defined identifier.</param>
    /// <param name="name">Optional display name for diagnostics and host UI.</param>
    /// <param name="tags">Optional host-defined metadata copied into the identity.</param>
    /// <returns>An immutable identity.</returns>
    /// <remarks>
    /// Tags are copied into a new dictionary. Use simple immutable tag values so
    /// diagnostics and host policy code can safely read them.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static Identity Create(
        string id,
        string? name = null,
        IReadOnlyDictionary<string, object>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return new Identity(id, name, CopyTags(tags));
    }

    private static IReadOnlyDictionary<string, object> CopyTags(IReadOnlyDictionary<string, object>? tags)
    {
        if (tags is null || tags.Count == 0)
            return EmptyTags.Instance;

        return new Dictionary<string, object>(tags, StringComparer.Ordinal);
    }
}

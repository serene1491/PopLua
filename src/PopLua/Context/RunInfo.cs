namespace PopLua.Context;

/// <summary>
/// Identifies one logical host run across its Lua execution steps.
/// </summary>
/// <remarks>
/// A session owns one run. Multiple calls in the same session therefore share
/// this value while receiving distinct <see cref="Step"/> values. Keep
/// product-specific evidence, URLs, and persistence in host services or tags.
/// </remarks>
public sealed class RunInfo
{
    private RunInfo(
        string id,
        string? attempt,
        string? parent,
        IReadOnlyDictionary<string, object> tags)
    {
        Id = id;
        Attempt = attempt;
        Parent = parent;
        Tags = tags;
    }

    /// <summary>
    /// Gets the stable host-defined logical run identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the optional host-defined attempt identifier.
    /// </summary>
    public string? Attempt { get; }

    /// <summary>
    /// Gets the optional parent run identifier.
    /// </summary>
    public string? Parent { get; }

    /// <summary>
    /// Gets a read-only copy of host-defined metadata associated with the run.
    /// </summary>
    public IReadOnlyDictionary<string, object> Tags { get; }

    /// <summary>
    /// Creates run information for a new logical execution.
    /// </summary>
    /// <param name="id">Stable host-defined logical run identifier.</param>
    /// <param name="attempt">Optional identifier for the current delivery or retry.</param>
    /// <param name="parent">Optional parent logical run identifier.</param>
    /// <param name="tags">Optional host metadata copied into the run.</param>
    /// <returns>Immutable run information.</returns>
    public static RunInfo Create(
        string id,
        string? attempt = null,
        string? parent = null,
        IReadOnlyDictionary<string, object>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return new RunInfo(
            id,
            EmptyToNull(attempt),
            EmptyToNull(parent),
            tags is null || tags.Count == 0
                ? EmptyTags.Instance
                : new Dictionary<string, object>(tags, StringComparer.Ordinal));
    }

    internal static RunInfo New()
        => new(Guid.NewGuid().ToString("N"), null, null, EmptyTags.Instance);

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}

namespace PopLua.Exceptions;

/// <summary>
/// Represents a failed conversion from a Lua value to a C# type.
/// </summary>
public sealed class NativeTypeException(string expected, ValueKind actual)
    : RuntimeException($"Expected {expected}, got {actual}.")
{
    /// <summary>
    /// Gets the expected Lua or host type description.
    /// </summary>
    public string Expected { get; } = expected;

    /// <summary>
    /// Gets the actual Lua value kind that was observed.
    /// </summary>
    public ValueKind Actual { get; } = actual;
}

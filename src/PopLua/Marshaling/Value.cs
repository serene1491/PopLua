using System.Runtime.InteropServices;

namespace PopLua.Marshaling;

/// <summary>
/// Represents a Lua value without boxing primitive values.
/// </summary>
/// <remarks>
/// `Value` is intended for generic values crossing the host/Lua boundary.
/// Table, function, and userdata values are opaque in this preview and are not
/// portable handles for later use outside the active call.
/// For generated userdata instance methods, the Lua receiver is consumed by
/// PopLua and exposed as the C# instance; do not model it as a
/// <c>Value self</c> parameter.
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
public readonly struct Value
{
    [FieldOffset(0)] private readonly long _int;
    [FieldOffset(0)] private readonly double _number;
    [FieldOffset(0)] private readonly bool _bool;
    [FieldOffset(8)] private readonly object? _object;
    [FieldOffset(16)] private readonly ValueKind _kind;

    private Value(ValueKind kind)
    {
        _int = 0;
        _number = 0;
        _bool = false;
        _object = null;
        _kind = kind;
    }

    private Value(bool value)
    {
        _int = 0;
        _number = 0;
        _object = null;
        _kind = ValueKind.Bool;
        _bool = value;
    }

    private Value(long value)
    {
        _number = 0;
        _bool = false;
        _object = null;
        _kind = ValueKind.Int;
        _int = value;
    }

    private Value(double value)
    {
        _int = 0;
        _bool = false;
        _object = null;
        _kind = ValueKind.Number;
        _number = value;
    }

    private Value(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _int = 0;
        _number = 0;
        _bool = false;
        _object = value;
        _kind = ValueKind.String;
    }

    /// <summary>
    /// Gets the value kind.
    /// </summary>
    public ValueKind Kind => _kind;

    /// <summary>
    /// Gets whether this value is Lua nil.
    /// </summary>
    public bool IsNil => _kind == ValueKind.Nil;

    /// <summary>
    /// Gets the nil value.
    /// </summary>
    public static Value Nil => new(ValueKind.Nil);

    internal static Value Opaque(ValueKind kind)
        => kind is ValueKind.Table or ValueKind.Function or ValueKind.Userdata
            ? new Value(kind)
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only opaque Lua value kinds can be created this way.");

    /// <summary>
    /// Creates a Lua boolean value.
    /// </summary>
    /// <param name="value">Boolean value to expose to Lua.</param>
    /// <returns>A Lua boolean value.</returns>
    public static Value From(bool value) => new(value);

    /// <summary>
    /// Creates a Lua integer value.
    /// </summary>
    /// <param name="value">Signed 64-bit integer value.</param>
    /// <returns>A Lua integer value.</returns>
    public static Value From(long value) => new(value);

    /// <summary>
    /// Creates a Lua integer value.
    /// </summary>
    /// <param name="value">Signed 32-bit integer value.</param>
    /// <returns>A Lua integer value.</returns>
    public static Value From(int value) => new((long)value);

    /// <summary>
    /// Creates a Lua integer value from an unsigned 32-bit integer.
    /// </summary>
    /// <param name="value">Unsigned 32-bit integer value.</param>
    /// <returns>A Lua integer value.</returns>
    public static Value From(uint value) => new(value);

    /// <summary>
    /// Creates a Lua integer value from an unsigned 64-bit integer when it fits in Lua's signed integer range.
    /// </summary>
    /// <param name="value">Unsigned 64-bit integer value.</param>
    /// <returns>A Lua integer value.</returns>
    /// <exception cref="OverflowException">Thrown when <paramref name="value"/> does not fit in Lua's signed integer range.</exception>
    public static Value From(ulong value) => new(checked((long)value));

    /// <summary>
    /// Creates a Lua number value.
    /// </summary>
    /// <param name="value">Double-precision number value.</param>
    /// <returns>A Lua number value.</returns>
    public static Value From(double value) => new(value);

    /// <summary>
    /// Creates a Lua number value from a single-precision value.
    /// </summary>
    /// <param name="value">Single-precision number value.</param>
    /// <returns>A Lua number value.</returns>
    public static Value From(float value) => new((double)value);

    /// <summary>
    /// Creates a Lua string value.
    /// </summary>
    /// <param name="value">String value to expose to Lua.</param>
    /// <returns>A Lua string value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static Value From(string value) => new(value);

    /// <summary>
    /// Tries to read this value as a Lua boolean.
    /// </summary>
    /// <param name="value">The boolean value when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when this value is a Lua boolean.</returns>
    public bool TryBool(out bool value)
    {
        value = _kind == ValueKind.Bool && _bool;
        return _kind == ValueKind.Bool;
    }

    /// <summary>
    /// Tries to read this value as a Lua integer.
    /// </summary>
    /// <param name="value">The integer value when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when this value is a Lua integer.</returns>
    public bool TryInt(out long value)
    {
        value = _kind == ValueKind.Int ? _int : 0;
        return _kind == ValueKind.Int;
    }

    /// <summary>
    /// Tries to read this value as a Lua number; integers are converted to numbers.
    /// </summary>
    /// <param name="value">The numeric value when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when this value is a Lua number or integer.</returns>
    public bool TryNumber(out double value)
    {
        if (_kind == ValueKind.Number)
        {
            value = _number;
            return true;
        }

        if (_kind == ValueKind.Int)
        {
            value = _int;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Tries to read this value as a Lua string.
    /// </summary>
    /// <param name="value">The string value when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when this value is a Lua string.</returns>
    public bool TryString(out string? value)
    {
        value = _kind == ValueKind.String ? (string?)_object : null;
        return _kind == ValueKind.String;
    }

    /// <summary>
    /// Reads this value as a Lua boolean or throws <see cref="NativeTypeException"/>.
    /// </summary>
    /// <returns>The boolean value.</returns>
    /// <exception cref="NativeTypeException">Thrown when this value is not a Lua boolean.</exception>
    public bool Bool()
    {
        if (TryBool(out var value))
            return value;

        throw new NativeTypeException("bool", Kind);
    }

    /// <summary>
    /// Reads this value as a Lua integer or throws <see cref="NativeTypeException"/>.
    /// </summary>
    /// <returns>The integer value.</returns>
    /// <exception cref="NativeTypeException">Thrown when this value is not a Lua integer.</exception>
    public long Int()
    {
        if (TryInt(out var value))
            return value;

        throw new NativeTypeException("int", Kind);
    }

    /// <summary>
    /// Reads this value as a Lua number or throws <see cref="NativeTypeException"/>.
    /// </summary>
    /// <returns>The number value.</returns>
    /// <exception cref="NativeTypeException">Thrown when this value is not a Lua number or integer.</exception>
    public double Number()
    {
        if (TryNumber(out var value))
            return value;

        throw new NativeTypeException("number", Kind);
    }

    /// <summary>
    /// Reads this value as a Lua string or throws <see cref="NativeTypeException"/>.
    /// </summary>
    /// <returns>The string value.</returns>
    /// <exception cref="NativeTypeException">Thrown when this value is not a Lua string.</exception>
    public string String()
    {
        if (TryString(out var value))
            return value!;

        throw new NativeTypeException("string", Kind);
    }
}

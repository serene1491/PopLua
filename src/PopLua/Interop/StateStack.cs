using System.Text;

namespace PopLua.Interop;

internal readonly ref struct StateStack
{
    private readonly StateHandle _state;

    internal StateStack(StateHandle state)
    {
        if (state.IsNull)
            throw new ArgumentException("Lua state cannot be null.", nameof(state));

        _state = state;
    }

    internal int Top => NativeApi.GetTop(_state.Value);

    internal StateHandle State => _state;

    internal NativeType TypeOf(int index) => NativeApi.Type(_state.Value, index);

    internal bool IsNil(int index) => TypeOf(index) == NativeType.Nil;

    internal void SetTop(int top) => NativeApi.SetTop(_state.Value, top);

    internal void Pop(int count = 1)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        NativeApi.SetTop(_state.Value, -count - 1);
    }

    internal void Remove(int index)
    {
        NativeApi.Rotate(_state.Value, index, -1);
        Pop();
    }

    internal void PushNil() => NativeApi.PushNil(_state.Value);

    internal void NewTable() => NativeApi.CreateTable(_state.Value, 0, 0);

    internal void PushBoolean(bool value) => NativeApi.PushBoolean(_state.Value, value ? 1 : 0);

    internal void PushInteger(long value) => NativeApi.PushInteger(_state.Value, value);

    internal void PushNumber(double value) => NativeApi.PushNumber(_state.Value, value);

    internal void PushString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);
        Span<byte> buffer = maxBytes <= 512 ? stackalloc byte[maxBytes] : new byte[maxBytes];
        var written = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);
        PushStringUtf8(buffer[..written]);
    }

    internal unsafe void PushStringUtf8(ReadOnlySpan<byte> value)
    {
        fixed (byte* ptr = value)
            NativeApi.PushString(_state.Value, ptr, (nuint)value.Length);
    }

    internal unsafe void SetField(int index, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var maxBytes = Encoding.UTF8.GetMaxByteCount(key.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = Encoding.UTF8.GetBytes(key.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.SetField(_state.Value, index, ptr);
    }

    internal bool ReadBoolean(int index) => NativeApi.ToBoolean(_state.Value, index) != 0;

    internal unsafe long ReadInteger(int index)
    {
        int isNumber = default;
        var value = NativeApi.ToInteger(_state.Value, index, &isNumber);

        if (isNumber == 0)
            throw new NativeTypeException("int", ToValueKind(TypeOf(index)));

        return value;
    }

    internal unsafe double ReadNumber(int index)
    {
        int isNumber = default;
        var value = NativeApi.ToNumber(_state.Value, index, &isNumber);

        if (isNumber == 0)
            throw new NativeTypeException("number", ToValueKind(TypeOf(index)));

        return value;
    }

    internal unsafe ReadOnlySpan<byte> ReadStringUtf8(int index)
    {
        nuint length = default;
        var ptr = NativeApi.ToString(_state.Value, index, &length);

        if (ptr is null)
            throw new NativeTypeException("string", ToValueKind(TypeOf(index)));

        return new ReadOnlySpan<byte>(ptr, checked((int)length));
    }

    internal string ReadString(int index)
        => Encoding.UTF8.GetString(ReadStringUtf8(index));

    internal Value ReadValue(int index)
        => TypeOf(index) switch
        {
            NativeType.Nil => Value.Nil,
            NativeType.Boolean => Value.From(ReadBoolean(index)),
            NativeType.Number => ReadNumberValue(index),
            NativeType.String => Value.From(ReadString(index)),
            NativeType.Table => Value.Opaque(ValueKind.Table),
            NativeType.Function => Value.Opaque(ValueKind.Function),
            NativeType.UserData => Value.Opaque(ValueKind.Userdata),
            var actual => throw new NativeTypeException("supported Lua value", ToValueKind(actual)),
        };

    internal void PushValue(Value value)
    {
        switch (value.Kind)
        {
            case ValueKind.Nil:
                PushNil();
                break;
            case ValueKind.Bool:
                PushBoolean(value.Bool());
                break;
            case ValueKind.Int:
                PushInteger(value.Int());
                break;
            case ValueKind.Number:
                PushNumber(value.Number());
                break;
            case ValueKind.String:
                PushString(value.String());
                break;
            default:
                throw new NativeTypeException("pushable Lua value", value.Kind);
        }
    }

    private Value ReadNumberValue(int index)
        => IsInteger(index) ? Value.From(ReadInteger(index)) : Value.From(ReadNumber(index));

    private bool IsInteger(int index)
        => NativeApi.IsInteger(_state.Value, index);

    private static ValueKind ToValueKind(NativeType type)
        => type switch
        {
            NativeType.Nil => ValueKind.Nil,
            NativeType.Boolean => ValueKind.Bool,
            NativeType.Number => ValueKind.Number,
            NativeType.String => ValueKind.String,
            NativeType.Table => ValueKind.Table,
            NativeType.Function => ValueKind.Function,
            NativeType.UserData => ValueKind.Userdata,
            _ => ValueKind.Nil,
        };
}

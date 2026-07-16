using System.ComponentModel;
using System.Text;

namespace PopLua.Binding;

/// <summary>
/// Registration surface used by generated modules.
/// </summary>
/// <remarks>
/// This type is public for source-generated binding code. Host applications
/// normally register modules through <see cref="ModuleCollection"/> rather
/// than constructing or calling this surface directly.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly unsafe ref struct Registration
{
    private readonly StateStack _stack;

    internal Registration(StateStack stack)
    {
        _stack = stack;
    }

    /// <summary>
    /// Registers a generated function directly in the Lua global namespace.
    /// </summary>
    /// <param name="name">Lua global function name.</param>
    /// <param name="function">Generated unmanaged callback function pointer.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public void GlobalFunction(string name, delegate* unmanaged[Cdecl]<nint, int> function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        NativeApi.PushCClosure(_stack.State.Value, function, 0);
        SetGlobal(name);
    }

    /// <summary>
    /// Registers a generated synchronous function in a Lua module table.
    /// </summary>
    /// <param name="moduleName">Lua module table name.</param>
    /// <param name="name">Function name inside the module table.</param>
    /// <param name="function">Generated unmanaged callback function pointer.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="moduleName"/> or <paramref name="name"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public void ModuleFunction(string moduleName, string name, delegate* unmanaged[Cdecl]<nint, int> function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        PushGlobal(moduleName);
        if (_stack.TypeOf(-1) != NativeType.Table)
        {
            _stack.Pop();
            _stack.NewTable();
            SetGlobal(moduleName);
            PushGlobal(moduleName);
        }

        NativeApi.PushCClosure(_stack.State.Value, function, 0);
        _stack.SetField(-2, name);
        _stack.Pop();
    }

    /// <summary>
    /// Registers a generated async module function through PopLua's coroutine wrapper.
    /// </summary>
    /// <param name="moduleName">Lua module table name.</param>
    /// <param name="name">Function name inside the module table.</param>
    /// <param name="function">Generated unmanaged callback function pointer that starts the async operation.</param>
    /// <remarks>
    /// The generated wrapper preserves synchronous-looking Lua calls while
    /// allowing PopLua to suspend and resume the owning coroutine.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="moduleName"/> or <paramref name="name"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public void AsyncModuleFunction(string moduleName, string name, delegate* unmanaged[Cdecl]<nint, int> function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        PushGlobal(moduleName);
        if (_stack.TypeOf(-1) != NativeType.Table)
        {
            _stack.Pop();
            _stack.NewTable();
            SetGlobal(moduleName);
            PushGlobal(moduleName);
        }

        Marshaller.PushAsyncFunction(_stack.State.Value, function);
        _stack.SetField(-2, name);
        _stack.Pop();
    }

    /// <summary>
    /// Registers a generated constant or property value in a Lua module table.
    /// </summary>
    /// <param name="moduleName">Lua module table name.</param>
    /// <param name="name">Value name inside the module table.</param>
    /// <param name="value">Lua value to store in the module table.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="moduleName"/> or <paramref name="name"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public void ModuleValue(string moduleName, string name, Value value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        PushGlobal(moduleName);
        if (_stack.TypeOf(-1) != NativeType.Table)
        {
            _stack.Pop();
            _stack.NewTable();
            SetGlobal(moduleName);
            PushGlobal(moduleName);
        }

        _stack.PushValue(value);
        _stack.SetField(-2, name);
        _stack.Pop();
    }

    /// <summary>
    /// Registers a generated computed-property indexer in a Lua module table.
    /// </summary>
    /// <param name="moduleName">Lua module table name.</param>
    /// <param name="index">Generated unmanaged callback used as the module table's <c>__index</c> metamethod.</param>
    /// <remarks>
    /// Generated bindings use this for module properties whose value depends
    /// on the active <see cref="ScriptContext"/>. Stored functions and values
    /// remain regular table fields; the indexer is called only for missing
    /// fields.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="moduleName"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public void ModuleComputedProperties(string moduleName, delegate* unmanaged[Cdecl]<nint, int> index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        PushGlobal(moduleName);
        if (_stack.TypeOf(-1) != NativeType.Table)
        {
            _stack.Pop();
            _stack.NewTable();
            SetGlobal(moduleName);
            PushGlobal(moduleName);
        }

        _stack.NewTable();
        NativeApi.PushCClosure(_stack.State.Value, index, 0);
        _stack.SetField(-2, "__index");
        NativeApi.SetMetaTable(_stack.State.Value, -2);
        _stack.Pop();
    }

    /// <summary>
    /// Registers a generated function on a userdata metatable.
    /// </summary>
    /// <param name="userdataName">Generated userdata metatable name.</param>
    /// <param name="name">Function or metamethod name to store on the metatable.</param>
    /// <param name="function">Generated unmanaged callback function pointer.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userdataName"/> or <paramref name="name"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public void UserdataMetatableFunction(string userdataName, string name, delegate* unmanaged[Cdecl]<nint, int> function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userdataName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        PushOrCreateMetatable(userdataName);
        NativeApi.PushCClosure(_stack.State.Value, function, 0);
        _stack.SetField(-2, name);
        _stack.Pop();
    }

    internal void SetGlobal(string name)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(name.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = Encoding.UTF8.GetBytes(name.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.SetGlobal(_stack.State.Value, ptr);
    }

    private void PushGlobal(string name)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(name.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = Encoding.UTF8.GetBytes(name.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.GetGlobal(_stack.State.Value, ptr);
    }

    private void PushOrCreateMetatable(string name)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(name.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = Encoding.UTF8.GetBytes(name.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.NewMetaTable(_stack.State.Value, ptr);
    }
}

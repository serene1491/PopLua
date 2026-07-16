using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace PopLua.Binding;

/// <summary>
/// Helper API used by generated binding code.
/// </summary>
/// <remarks>
/// This surface is public only because source-generated code lives in consumer
/// assemblies. Normal host applications should not call it directly, and native
/// Lua state handles remain an implementation detail.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class Marshaller
{
    private const int AsyncDirectVoid = 0;
    private const int AsyncDirectValues = 1;
    private const int AsyncToken = 2;

    private static readonly byte[] AsyncWrapper = """
        local raw, is_ready, yield, is_success, error_message, take_result, fail, release = ...
        local lua_error = error

        local function finish(state, first, ...)
            if state == 0 then
                return
            end

            if state == 1 then
                return first, ...
            end

            local token = first
            if not is_ready(token) then
                yield(token)
            end

            if is_success(token) then
                return take_result(token)
            end

            local message = error_message(token)
            if lua_error ~= nil then
                release(token)
                lua_error(message, 0)
            end

            return fail(token)
        end

        return function(...)
            return finish(raw(...))
        end
        """u8.ToArray();

    private static readonly byte[] AsyncWrapperName = "poplua:async-wrapper\0"u8.ToArray();
    private static readonly byte[] AsyncWrapperRegistryKey = "poplua:async-wrapper:factory\0"u8.ToArray();

    /// <summary>
    /// Gets the current Lua execution context for generated callbacks.
    /// </summary>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <returns>The current execution context, or an anonymous fallback outside an active execution.</returns>
    public static ScriptContext Context(nint state)
        => Session.CurrentContext ?? ScriptContext.Create();

    /// <summary>
    /// Reads a Lua boolean argument for generated binding code.
    /// </summary>
    public static bool ReadBool(nint state, int index)
        => new StateStack(new StateHandle(state)).ReadBoolean(index);

    /// <summary>
    /// Reads a Lua integer argument as <see cref="int"/>.
    /// </summary>
    public static int ReadInt(nint state, int index)
        => checked((int)new StateStack(new StateHandle(state)).ReadInteger(index));

    /// <summary>
    /// Reads a Lua integer argument as <see cref="uint"/>.
    /// </summary>
    public static uint ReadUInt(nint state, int index)
        => checked((uint)new StateStack(new StateHandle(state)).ReadInteger(index));

    /// <summary>
    /// Reads a Lua integer argument as <see cref="long"/>.
    /// </summary>
    public static long ReadLong(nint state, int index)
        => new StateStack(new StateHandle(state)).ReadInteger(index);

    /// <summary>
    /// Reads a Lua integer argument as <see cref="ulong"/>.
    /// </summary>
    public static ulong ReadULong(nint state, int index)
        => checked((ulong)new StateStack(new StateHandle(state)).ReadInteger(index));

    /// <summary>
    /// Reads a Lua number argument as <see cref="float"/>.
    /// </summary>
    public static float ReadFloat(nint state, int index)
        => checked((float)new StateStack(new StateHandle(state)).ReadNumber(index));

    /// <summary>
    /// Reads a Lua number argument as <see cref="double"/>.
    /// </summary>
    public static double ReadDouble(nint state, int index)
        => new StateStack(new StateHandle(state)).ReadNumber(index);

    /// <summary>
    /// Reads a Lua string argument as UTF-8 text.
    /// </summary>
    public static string ReadString(nint state, int index)
        => new StateStack(new StateHandle(state)).ReadString(index);

    /// <summary>
    /// Reads a Lua value argument into a <see cref="Value"/>.
    /// </summary>
    public static Value ReadValue(nint state, int index)
        => new StateStack(new StateHandle(state)).ReadValue(index);

    /// <summary>
    /// Gets the current Lua stack top for generated binding cleanup.
    /// </summary>
    public static int Top(nint state)
        => NativeApi.GetTop(state);

    /// <summary>
    /// Restores the Lua stack top for generated binding cleanup.
    /// </summary>
    public static void SetTop(nint state, int top)
        => NativeApi.SetTop(state, top);

    /// <summary>
    /// Pops values from the Lua stack for generated binding code.
    /// </summary>
    public static void Pop(nint state, int count = 1)
        => new StateStack(new StateHandle(state)).Pop(count);

    /// <summary>
    /// Verifies that a Lua argument is a table.
    /// </summary>
    public static void ExpectTable(nint state, int index, string expected)
    {
        var actual = NativeApi.Type(state, index);
        if (actual != NativeType.Table)
            throw new NativeTypeException(expected, ToValueKind(actual));
    }

    /// <summary>Rejects unknown named fields in a generated descriptor table.</summary>
    public static void ValidateFields(nint state, int index, string path, params string[] allowed)
    {
        ExpectTable(state, index, path);
        var absolute = NativeApi.AbsIndex(state, index);
        var top = NativeApi.GetTop(state);
        try
        {
            NativeApi.PushNil(state);
            while (NativeApi.Next(state, absolute) != 0)
            {
                if (NativeApi.Type(state, -2) != NativeType.String)
                    throw new ScriptException($"unknown descriptor field: {path} requires named fields");
                var key = ReadString(state, -2);
                if (Array.IndexOf(allowed, key) < 0)
                    throw new ScriptException($"unknown descriptor field: {path}.{key}");
                NativeApi.SetTop(state, NativeApi.GetTop(state) - 1);
            }
        }
        finally
        {
            NativeApi.SetTop(state, top);
        }
    }

    /// <summary>Validates one dense one-based Lua array and returns its length.</summary>
    public static int ValidateArray(nint state, int index, string path)
    {
        ExpectTable(state, index, path);
        var absolute = NativeApi.AbsIndex(state, index);
        var count = RawLength(state, absolute);
        var top = NativeApi.GetTop(state);
        var seen = 0;
        try
        {
            NativeApi.PushNil(state);
            while (NativeApi.Next(state, absolute) != 0)
            {
                if (NativeApi.Type(state, -2) != NativeType.Number || !NativeApi.IsInteger(state, -2))
                    throw new ScriptException($"invalid descriptor field: {path} must be a dense array");

                var key = ReadLong(state, -2);
                if (key < 1 || key > count)
                    throw new ScriptException($"invalid descriptor field: {path} must be a dense array");

                seen++;
                NativeApi.SetTop(state, NativeApi.GetTop(state) - 1);
            }

            if (seen != count)
                throw new ScriptException($"invalid descriptor field: {path} must be a dense array");
        }
        finally
        {
            NativeApi.SetTop(state, top);
        }

        return count;
    }

    /// <summary>Reads one dense Lua array of UTF-8 strings.</summary>
    public static List<string> ReadStringList(nint state, int index, string path)
    {
        var absolute = NativeApi.AbsIndex(state, index);
        var count = ValidateArray(state, absolute, path);
        var values = new List<string>(count);
        for (var i = 1; i <= count; i++)
        {
            PushArrayItem(state, absolute, i);
            try
            {
                values.Add(ReadString(state, -1));
            }
            catch (Exception error)
            {
                throw new ScriptException($"invalid descriptor field: {path}[{i}]: {error.Message}");
            }
            finally
            {
                Pop(state);
            }
        }
        return values;
    }

    /// <summary>
    /// Pushes a named table field onto the stack for generated descriptor readers.
    /// </summary>
    public static unsafe bool PushField(nint state, int index, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var maxBytes = Encoding.UTF8.GetMaxByteCount(key.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = Encoding.UTF8.GetBytes(key.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            return NativeApi.GetField(state, index, ptr) != NativeType.Nil;
    }

    /// <summary>
    /// Pushes an array entry from a Lua table onto the stack for generated descriptor readers.
    /// </summary>
    public static void PushArrayItem(nint state, int index, long n)
        => NativeApi.RawGetI(state, index, n);

    /// <summary>
    /// Reads the raw Lua length of a table for generated descriptor readers.
    /// </summary>
    public static int RawLength(nint state, int index)
        => checked((int)NativeApi.RawLen(state, index));

    /// <summary>
    /// Creates a Lua table for a generated copy-based DTO writer.
    /// </summary>
    public static void CreateTable(nint state, int arrayCount, int fieldCount)
        => NativeApi.CreateTable(state, arrayCount, fieldCount);

    /// <summary>
    /// Stores the value at the top of the stack in a generated table field.
    /// </summary>
    public static unsafe void SetField(nint state, int index, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var maxBytes = Encoding.UTF8.GetMaxByteCount(key.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = Encoding.UTF8.GetBytes(key.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.SetField(state, index, ptr);
    }

    /// <summary>
    /// Stores the value at the top of the stack in a generated one-based array.
    /// </summary>
    public static void SetArrayItem(nint state, int index, long itemIndex)
        => NativeApi.RawSetI(state, index, itemIndex);

    /// <summary>
    /// Copies a bounded managed string list into a fresh Lua array.
    /// </summary>
    public static void PushStringList(nint state, IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            Push(state, Value.Nil);
            return;
        }

        CreateTable(state, values.Count, 0);
        for (var index = 0; index < values.Count; index++)
        {
            Push(state, values[index]);
            SetArrayItem(state, -2, index + 1);
        }
    }

    /// <summary>
    /// Captures a Lua function argument as a session-owned reference.
    /// </summary>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="index">Lua stack index containing the function.</param>
    /// <returns>A function reference owned by the active <see cref="Session"/>.</returns>
    /// <remarks>
    /// The returned reference must be disposed by the host and cannot be used
    /// after its owning session is disposed.
    /// </remarks>
    public static FunctionRef ReadFunctionRef(nint state, int index)
        => Session.CaptureFunction(state, index);

    /// <summary>
    /// Reads all remaining Lua arguments from the specified stack index.
    /// </summary>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="startIndex">One-based Lua stack index of the first variadic argument.</param>
    /// <returns>All remaining arguments converted to <see cref="Value"/> values.</returns>
    public static Value[] ReadRest(nint state, int startIndex)
    {
        var stack = new StateStack(new StateHandle(state));
        var count = Math.Max(0, stack.Top - startIndex + 1);
        var values = new Value[count];

        for (var i = 0; i < values.Length; i++)
            values[i] = stack.ReadValue(startIndex + i);

        return values;
    }

    /// <summary>
    /// Reads generated userdata after verifying its PopLua metatable.
    /// </summary>
    /// <typeparam name="T">Expected managed userdata type.</typeparam>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="index">Lua stack index to read.</param>
    /// <param name="metatableName">Generated userdata metatable name.</param>
    /// <returns>The managed object stored in the userdata wrapper.</returns>
    /// <exception cref="NativeTypeException">Thrown when the value is not userdata with the expected PopLua metatable.</exception>
    public static T ReadUserdata<T>(nint state, int index, string metatableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metatableName);

        var handle = ReadUserdataHandle(state, index, metatableName);
        if (handle.Target is T value)
            return value;

        if (handle.Target is null)
            throw new NativeTypeException(metatableName, ValueKind.Nil);

        return (T)handle.Target;
    }

    /// <summary>
    /// Checks whether a Lua string equals the expected UTF-8 text without allocating a managed string.
    /// </summary>
    public static bool StringEquals(nint state, int index, byte[] expectedUtf8)
    {
        ArgumentNullException.ThrowIfNull(expectedUtf8);

        var stack = new StateStack(new StateHandle(state));
        var actual = stack.ReadStringUtf8(index);
        return actual.SequenceEqual(expectedUtf8);
    }

    /// <summary>
    /// Pushes a boolean return value onto the Lua stack.
    /// </summary>
    public static void Push(nint state, bool value)
        => new StateStack(new StateHandle(state)).PushBoolean(value);

    /// <summary>
    /// Pushes an integer return value onto the Lua stack.
    /// </summary>
    public static void Push(nint state, int value)
        => new StateStack(new StateHandle(state)).PushInteger(value);

    /// <summary>
    /// Pushes an unsigned integer return value onto the Lua stack.
    /// </summary>
    public static void Push(nint state, uint value)
        => new StateStack(new StateHandle(state)).PushInteger(value);

    /// <summary>
    /// Pushes a 64-bit integer return value onto the Lua stack.
    /// </summary>
    public static void Push(nint state, long value)
        => new StateStack(new StateHandle(state)).PushInteger(value);

    /// <summary>
    /// Pushes an unsigned 64-bit integer return value onto the Lua stack.
    /// </summary>
    public static void Push(nint state, ulong value)
        => new StateStack(new StateHandle(state)).PushInteger(checked((long)value));

    /// <summary>
    /// Pushes a single-precision number return value onto the Lua stack.
    /// </summary>
    public static void Push(nint state, float value)
        => new StateStack(new StateHandle(state)).PushNumber(value);

    /// <summary>
    /// Pushes a number return value onto the Lua stack.
    /// </summary>
    public static void Push(nint state, double value)
        => new StateStack(new StateHandle(state)).PushNumber(value);

    /// <summary>
    /// Pushes a string return value, or Lua nil when the value is <see langword="null"/>.
    /// </summary>
    public static void Push(nint state, string? value)
    {
        var stack = new StateStack(new StateHandle(state));
        if (value is null)
            stack.PushNil();
        else
            stack.PushString(value);
    }

    /// <summary>
    /// Pushes a generic Lua value onto the Lua stack.
    /// </summary>
    public static void Push(nint state, Value value)
        => new StateStack(new StateHandle(state)).PushValue(value);

    /// <summary>
    /// Pushes a generated native Lua callback onto the Lua stack.
    /// </summary>
    public static unsafe void PushFunction(nint state, delegate* unmanaged[Cdecl]<nint, int> function)
        => NativeApi.PushCClosure(state, function, 0);

    /// <summary>
    /// Pushes PopLua's generated async wrapper around a raw generated callback.
    /// </summary>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="function">Generated callback that starts the async operation and returns the wrapper protocol values.</param>
    /// <remarks>
    /// This is used by generated module and userdata bindings. The wrapper
    /// yields with PopLua's hidden async token when the returned operation is
    /// incomplete; generated callbacks themselves must not call Lua yield APIs.
    /// </remarks>
    public static unsafe void PushAsyncFunction(nint state, delegate* unmanaged[Cdecl]<nint, int> function)
    {
        PushAsyncWrapperFactory(state);

        NativeApi.PushCClosure(state, function, 0);
        NativeApi.PushCClosure(state, &PendingOperation.IsReadyCallback, 0);
        PendingOperation.PushYield(state);
        NativeApi.PushCClosure(state, &PendingOperation.IsSuccessCallback, 0);
        NativeApi.PushCClosure(state, &PendingOperation.ErrorMessageCallback, 0);
        NativeApi.PushCClosure(state, &PendingOperation.TakeResultCallback, 0);
        NativeApi.PushCClosure(state, &PendingOperation.FailCallback, 0);
        NativeApi.PushCClosure(state, &PendingOperation.ReleaseCallback, 0);

        var callStatus = NativeApi.PCall(state, 8, 1, errorFunction: 0, context: 0, continuation: 0);
        if (callStatus != 0)
            throw new ScriptException(ReadErrorMessage(state));
    }

    private static unsafe void PushAsyncWrapperFactory(nint state)
    {
        fixed (byte* keyPtr = AsyncWrapperRegistryKey)
        {
            if (NativeApi.GetField(state, NativeApi.RegistryIndex, keyPtr) == NativeType.Function)
                return;
        }

        NativeApi.SetTop(state, -2);

        fixed (byte* wrapperPtr = AsyncWrapper)
        fixed (byte* namePtr = AsyncWrapperName)
        {
            var status = NativeApi.LoadBuffer(state, wrapperPtr, (nuint)AsyncWrapper.Length, namePtr, null);
            if (status != 0)
                throw new ScriptException(ReadErrorMessage(state));
        }

        fixed (byte* keyPtr = AsyncWrapperRegistryKey)
        {
            NativeApi.PushValue(state, -1);
            NativeApi.SetField(state, NativeApi.RegistryIndex, keyPtr);
        }
    }

    /// <summary>
    /// Pushes a generated userdata wrapper and attaches the registered metatable.
    /// </summary>
    /// <typeparam name="T">Managed userdata type.</typeparam>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="value">Managed value to wrap; <see langword="null"/> is pushed as Lua nil.</param>
    /// <param name="metatableName">Generated userdata metatable name.</param>
    /// <remarks>
    /// Non-null values allocate a Lua userdata wrapper and a managed
    /// <see cref="GCHandle"/> released by generated finalization.
    /// </remarks>
    public static unsafe void PushUserdata<T>(nint state, T value, string metatableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metatableName);

        if (value is null)
        {
            new StateStack(new StateHandle(state)).PushNil();
            return;
        }

        var storage = (nint*)NativeApi.NewUserData(state, (nuint)sizeof(nint), 0);
        *storage = 0;

        var handle = GCHandle.Alloc(value);
        *storage = GCHandle.ToIntPtr(handle);

        PushMetatable(state, metatableName);
        NativeApi.SetMetaTable(state, -2);
    }

    /// <summary>
    /// Pushes multiple Lua return values and returns the number of values pushed.
    /// </summary>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="values">Return values to push in order. An empty array returns no Lua values.</param>
    /// <returns>The number of values pushed.</returns>
    public static int PushMany(nint state, Value[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var stack = new StateStack(new StateHandle(state));
        foreach (var value in values)
            stack.PushValue(value);

        return values.Length;
    }

    /// <summary>
    /// Pushes an async operation token for a generated async binding with no return value.
    /// </summary>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="task">Task returned by the generated async binding.</param>
    /// <param name="pauseTime">Whether suspended async time pauses active-time quota accounting.</param>
    /// <returns>The number of Lua values pushed for the async wrapper protocol.</returns>
    public static int BeginAsync(nint state, ValueTask task, bool pauseTime = true)
    {
        Push(state, AsyncToken);
        PendingOperation.PushToken(state, new PendingOperationVoid(task, pauseTime));
        return 2;
    }

    /// <summary>
    /// Pushes an async operation token for a generated async binding with a return value.
    /// </summary>
    /// <typeparam name="T">Async result type.</typeparam>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="task">Task returned by the generated async binding.</param>
    /// <param name="pusher">Generated pusher used when the async result becomes available.</param>
    /// <param name="pauseTime">Whether suspended async time pauses active-time quota accounting.</param>
    /// <returns>The number of Lua values pushed for the async wrapper protocol.</returns>
    public static int BeginAsync<T>(nint state, ValueTask<T> task, Func<nint, T, int> pusher, bool pauseTime = true)
    {
        ArgumentNullException.ThrowIfNull(pusher);

        Push(state, AsyncToken);
        PendingOperation.PushToken(state, new PendingOperation<T>(task, pusher, pauseTime));
        return 2;
    }

    /// <summary>
    /// Pushes the direct-completion marker for a generated async binding with no return value.
    /// </summary>
    public static int CompleteAsync(nint state)
    {
        Push(state, AsyncDirectVoid);
        return 1;
    }

    /// <summary>
    /// Pushes direct-completion values for a generated async binding with a return value.
    /// </summary>
    public static int CompleteAsync<T>(nint state, T result, Func<nint, T, int> pusher)
    {
        ArgumentNullException.ThrowIfNull(pusher);

        var stack = new StateStack(new StateHandle(state));
        var top = stack.Top;
        try
        {
            Push(state, AsyncDirectValues);
            return pusher(state, result) + 1;
        }
        catch
        {
            stack.SetTop(top);
            throw;
        }
    }

    /// <summary>
    /// Pushes a completed failed async operation token for generated exception handling.
    /// </summary>
    public static int BeginFailedAsync(nint state, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        Push(state, AsyncToken);
        PendingOperation.PushToken(state, new FailedPendingOperation(error));
        return 2;
    }

    /// <summary>
    /// Releases the managed handle stored in generated userdata during Lua finalization.
    /// </summary>
    public static int FreeUserdata(nint state, int index, string metatableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metatableName);

        var handle = ReadUserdataHandle(state, index, metatableName);
        if (handle.IsAllocated)
            handle.Free();

        ClearUserdataHandle(state, index, metatableName);
        return 0;
    }

    /// <summary>
    /// Records a managed callback error so the active Lua execution can fail consistently.
    /// </summary>
    /// <param name="state">Native Lua state pointer supplied by generated callbacks.</param>
    /// <param name="message">Error message returned to the host as a <see cref="ScriptException"/>.</param>
    /// <returns>Zero Lua return values.</returns>
    /// <remarks>
    /// This does not call Lua's `error` API because PopLua avoids native
    /// longjmp across managed callback frames.
    /// </remarks>
    public static int Error(nint state, string message)
    {
        Session.SetManagedError(state, new ScriptException(message));
        return 0;
    }

    /// <summary>Records a managed callback exception while preserving its host cause.</summary>
    public static int Error(nint state, Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Session.SetManagedError(state, error);
        return 0;
    }

    private static unsafe GCHandle ReadUserdataHandle(nint state, int index, string metatableName)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(metatableName.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var length = WriteNullTerminatedUtf8(metatableName, buffer);

        fixed (byte* namePtr = buffer[..length])
        {
            var storage = (nint*)NativeApi.TestUserData(state, index, namePtr);
            if (storage is null || *storage == 0)
                throw new NativeTypeException(metatableName, ToValueKind(NativeApi.Type(state, index)));

            return GCHandle.FromIntPtr(*storage);
        }
    }

    private static string ReadErrorMessage(nint state)
    {
        var stack = new StateStack(new StateHandle(state));
        var message = stack.TypeOf(-1) == NativeType.String
            ? stack.ReadString(-1)
            : "Lua registration failed.";

        if (stack.Top > 0)
            stack.Pop();

        return message;
    }

    private static unsafe void ClearUserdataHandle(nint state, int index, string metatableName)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(metatableName.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var length = WriteNullTerminatedUtf8(metatableName, buffer);

        fixed (byte* namePtr = buffer[..length])
        {
            var storage = (nint*)NativeApi.TestUserData(state, index, namePtr);
            if (storage is not null)
                *storage = 0;
        }
    }

    private static unsafe void PushMetatable(nint state, string metatableName)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(metatableName.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var length = WriteNullTerminatedUtf8(metatableName, buffer);

        fixed (byte* namePtr = buffer[..length])
            NativeApi.GetField(state, NativeApi.RegistryIndex, namePtr);
    }

    private static int WriteNullTerminatedUtf8(string value, Span<byte> destination)
    {
        var written = Encoding.UTF8.GetBytes(value.AsSpan(), destination);
        destination[written] = 0;
        return written + 1;
    }

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

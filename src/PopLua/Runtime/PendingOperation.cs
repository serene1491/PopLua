using System.Runtime.InteropServices;
using System.Text;

namespace PopLua.Runtime;

internal abstract class PendingOperation
{
    internal const string MetatableName = "PopLua.PendingOperation";


    private Exception? _error;
    private bool _completed;
    private bool _success;

    protected PendingOperation(bool pauseActiveTime)
    {
        PauseActiveTime = pauseActiveTime;
    }

    internal bool PauseActiveTime { get; }
    internal bool IsCompleted => _completed;
    internal bool IsSuccess => _completed && _success;
    internal Exception? Error => _error;

    internal async ValueTask<bool> WaitAsync(CancellationToken cancellation)
    {
        if (_completed)
            return true;

        try
        {
            await WaitCoreAsync(cancellation).ConfigureAwait(false);
            CompleteSuccess();
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            CompleteFailure(ex);
            return true;
        }
    }

    internal abstract int PushResult(nint state);

    protected abstract ValueTask WaitCoreAsync(CancellationToken cancellation);

    protected void CompleteSuccess()
    {
        _success = true;
        _completed = true;
    }

    protected void CompleteFailure(Exception error)
    {
        _error = error;
        _success = false;
        _completed = true;
    }

    internal static unsafe void Register(StateStack stack)
    {
        var name = NullTerminatedUtf8(MetatableName);
        fixed (byte* namePtr = name)
            NativeApi.NewMetaTable(stack.State.Value, namePtr);

        NativeApi.PushCClosure(stack.State.Value, &Gc, 0);
        stack.SetField(-2, "__gc");
        stack.Pop();
    }

    internal static unsafe void PushToken(nint state, PendingOperation operation)
    {
        var storage = (nint*)NativeApi.NewUserData(state, (nuint)sizeof(nint), 0);
        *storage = 0;

        var handle = GCHandle.Alloc(operation);
        *storage = GCHandle.ToIntPtr(handle);

        var name = NullTerminatedUtf8(MetatableName);
        fixed (byte* namePtr = name)
            NativeApi.GetField(state, NativeApi.RegistryIndex, namePtr);

        NativeApi.SetMetaTable(state, -2);
    }

    internal static unsafe PendingOperation Read(nint state, int index)
    {
        var name = NullTerminatedUtf8(MetatableName);
        fixed (byte* namePtr = name)
        {
            var storage = (nint*)NativeApi.TestUserData(state, index, namePtr);
            if (storage is null || *storage == 0)
                throw new ScriptException("PopLua async operation token expected.");

            if (GCHandle.FromIntPtr(*storage).Target is PendingOperation operation)
                return operation;
        }

        throw new ScriptException("PopLua async operation token is invalid.");
    }

    internal static unsafe void PushYield(nint state)
    {
        NativeApi.OpenCoroutine(state);

        var yield = NullTerminatedUtf8("yield");
        fixed (byte* yieldPtr = yield)
            NativeApi.GetField(state, -1, yieldPtr);

        new StateStack(new StateHandle(state)).Remove(-2);
    }

    internal static string ErrorMessage(PendingOperation operation)
        => operation.Error?.Message ?? "Lua async operation failed.";

    internal static void ReleaseToken(nint state, int index)
    {
        try
        {
            FreeToken(state, index);
        }
        catch
        {
            // Token cleanup must not surface into Lua finalization.
        }
    }

    private static unsafe void FreeToken(nint state, int index)
    {
        var name = NullTerminatedUtf8(MetatableName);
        fixed (byte* namePtr = name)
        {
            var storage = (nint*)NativeApi.TestUserData(state, index, namePtr);
            if (storage is null || *storage == 0)
                return;

            var handle = GCHandle.FromIntPtr(*storage);
            if (handle.IsAllocated)
                handle.Free();

            *storage = 0;
        }
    }

    private static byte[] NullTerminatedUtf8(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var bytes = new byte[byteCount + 1];
        Encoding.UTF8.GetBytes(value, 0, value.Length, bytes, 0);
        return bytes;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int IsReadyCallback(nint state)
    {
        try
        {
            Marshaller.Push(state, Read(state, 1).IsCompleted);
            return 1;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int IsSuccessCallback(nint state)
    {
        try
        {
            Marshaller.Push(state, Read(state, 1).IsSuccess);
            return 1;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int ErrorMessageCallback(nint state)
    {
        try
        {
            Marshaller.Push(state, ErrorMessage(Read(state, 1)));
            return 1;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int TakeResultCallback(nint state)
    {
        try
        {
            try
            {
                return Read(state, 1).PushResult(state);
            }
            finally
            {
                ReleaseToken(state, 1);
            }
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int FailCallback(nint state)
    {
        try
        {
            try
            {
                var operation = Read(state, 1);
                Session.SetManagedError(state, operation.Error ?? new ScriptException(ErrorMessage(operation)));
            }
            finally
            {
                ReleaseToken(state, 1);
            }

            return 0;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int ReleaseCallback(nint state)
    {
        ReleaseToken(state, 1);
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int Gc(nint state)
    {
        ReleaseToken(state, 1);
        return 0;
    }
}

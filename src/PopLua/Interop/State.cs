using System.Runtime.InteropServices;

namespace PopLua.Interop;

internal sealed class State : IDisposable
{
    private nint _state;
    private GCHandle _allocatorHandle;

    private State(nint state, AllocatorTracker? allocator)
    {
        _state = state;
        Allocator = allocator;
    }

    internal AllocatorTracker? Allocator { get; }

    internal StateHandle Handle
    {
        get
        {
            ThrowIfDisposed();
            return new StateHandle(_state);
        }
    }

    internal static State Create(Library libs = Library.None, AllocatorOptions allocator = default)
    {
        AllocatorTracker? tracker = null;
        GCHandle handle = default;
        nint state;

        if (allocator.MaxHeapBytes > 0 || allocator.GcThresholdBytes > 0)
        {
            tracker = new AllocatorTracker(allocator.MaxHeapBytes, allocator.GcThresholdBytes);
            handle = GCHandle.Alloc(tracker);

            unsafe
            {
                state = NativeApi.NewState(&Allocate, GCHandle.ToIntPtr(handle));
            }
        }
        else
        {
            state = NativeApi.NewState();
        }

        if (state == 0)
        {
            if (handle.IsAllocated)
                handle.Free();

            throw new InvalidOperationException("Lua state could not be created.");
        }

        OpenLibs(state, libs);

        return new State(state, tracker)
        {
            _allocatorHandle = handle,
        };
    }

    internal StateStack Stack
    {
        get
        {
            ThrowIfDisposed();
            return new StateStack(new StateHandle(_state));
        }
    }

    public void Dispose()
    {
        var state = Interlocked.Exchange(ref _state, 0);
        if (state != 0)
            NativeApi.Close(state);

        if (_allocatorHandle.IsAllocated)
            _allocatorHandle.Free();
    }

    private void ThrowIfDisposed()
    {
        if (_state == 0)
            throw new ObjectDisposedException(nameof(State));
    }

    private static void OpenLibs(nint state, Library libraries)
    {
        if (libraries == Library.None)
            return;

        if (libraries == Library.All)
        {
            NativeApi.OpenLibs(state);
            return;
        }

        if ((libraries & Library.FullBase) != 0)
        {
            NativeApi.OpenBase(state);
            Pop(state);
        }
        else if ((libraries & Library.SafeBase) != 0)
        {
            NativeApi.OpenBase(state);
            Pop(state);
            RemoveUnsafeBaseGlobals(state);
        }

        if ((libraries & Library.Package) != 0)
        {
            NativeApi.OpenPackage(state);
            SetGlobal(state, "package");
        }

        if ((libraries & Library.Coroutine) != 0)
        {
            NativeApi.OpenCoroutine(state);
            SetGlobal(state, "coroutine");
        }

        if ((libraries & Library.Table) != 0)
        {
            NativeApi.OpenTable(state);
            SetGlobal(state, "table");
        }

        if ((libraries & Library.Io) != 0)
        {
            NativeApi.OpenIo(state);
            SetGlobal(state, "io");
        }

        if ((libraries & Library.Os) != 0)
        {
            NativeApi.OpenOs(state);
            SetGlobal(state, "os");
        }

        if ((libraries & Library.String) != 0)
        {
            NativeApi.OpenString(state);
            SetGlobal(state, "string");
        }

        if ((libraries & Library.Math) != 0)
        {
            NativeApi.OpenMath(state);
            SetGlobal(state, "math");
        }

        if ((libraries & Library.Utf8) != 0)
        {
            NativeApi.OpenUtf8(state);
            SetGlobal(state, "utf8");
        }

        if ((libraries & Library.Debug) != 0)
        {
            NativeApi.OpenDebug(state);
            SetGlobal(state, "debug");
        }
    }

    private static void RemoveUnsafeBaseGlobals(nint state)
    {
        SetGlobalNil(state, "_G");
        SetGlobalNil(state, "_VERSION");
        SetGlobalNil(state, "collectgarbage");
        SetGlobalNil(state, "dofile");
        SetGlobalNil(state, "getmetatable");
        SetGlobalNil(state, "load");
        SetGlobalNil(state, "loadfile");
        SetGlobalNil(state, "next");
        SetGlobalNil(state, "print");
        SetGlobalNil(state, "rawequal");
        SetGlobalNil(state, "rawget");
        SetGlobalNil(state, "rawlen");
        SetGlobalNil(state, "rawset");
        SetGlobalNil(state, "setmetatable");
        SetGlobalNil(state, "warn");
        SetGlobalNil(state, "xpcall");
    }

    private static void Pop(nint state)
        => NativeApi.SetTop(state, -2);

    private static unsafe void SetGlobal(nint state, string name)
    {
        var maxBytes = System.Text.Encoding.UTF8.GetMaxByteCount(name.Length);
        Span<byte> buffer = maxBytes + 1 <= 64 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = System.Text.Encoding.UTF8.GetBytes(name.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.SetGlobal(state, ptr);
    }

    private static unsafe void SetGlobalNil(nint state, string name)
    {
        NativeApi.PushNil(state);

        var maxBytes = System.Text.Encoding.UTF8.GetMaxByteCount(name.Length);
        Span<byte> buffer = maxBytes + 1 <= 64 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = System.Text.Encoding.UTF8.GetBytes(name.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.SetGlobal(state, ptr);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static unsafe nint Allocate(nint userData, nint pointer, nuint oldSize, nuint newSize)
    {
        var tracker = (AllocatorTracker)GCHandle.FromIntPtr(userData).Target!;

        if (newSize == 0)
        {
            if (pointer != 0)
            {
                tracker.Free(oldSize);
                NativeMemory.Free((void*)pointer);
            }

            return 0;
        }

        var trackedOldSize = pointer == 0 ? 0 : oldSize;
        if (!tracker.TryResize(trackedOldSize, newSize))
            return 0;

        var newPointer = NativeMemory.Realloc((void*)pointer, newSize);
        if (newPointer is null)
        {
            tracker.RestoreAfterFailedResize(trackedOldSize, newSize);
            return 0;
        }

        return (nint)newPointer;
    }
}

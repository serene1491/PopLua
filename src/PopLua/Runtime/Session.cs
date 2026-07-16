using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PopLua.Runtime;

/// <summary>
/// Stateful Lua execution scope. Not thread-safe.
/// </summary>
/// <remarks>
/// A session owns one Lua state and the globals inside that state. Dispose the
/// session when execution is finished. Create separate sessions for concurrent
/// execution or for isolated trigger runs; reuse a session only when scripts
/// should intentionally share Lua globals.
/// </remarks>
public sealed class Session : IAsyncDisposable
{
    private const int OkStatus = 0;
    private const int YieldStatus = 1;
    private const int GcCollect = 2;
    private const int HookCall = 0;
    private const int HookReturn = 1;
    private const int HookCount = 3;
    private const int HookTailCall = 4;
    private const int MaskCall = 1;
    private const int MaskReturn = 2;
    private const int MaskCount = 8;

    private static readonly byte[] QuotaInstructions = "poplua:quota:instructions"u8.ToArray();
    private static readonly byte[] QuotaActiveTime = "poplua:quota:active-time"u8.ToArray();
    private static readonly byte[] QuotaWallTime = "poplua:quota:wall-time"u8.ToArray();
    private static readonly byte[] QuotaMemory = "poplua:quota:memory"u8.ToArray();
    private static readonly byte[] QuotaCallDepth = "poplua:quota:call-depth"u8.ToArray();
    private static readonly byte[] ExecutionCanceled = "poplua:canceled"u8.ToArray();
    private static readonly byte[] TracebackField = "traceback\0"u8.ToArray();
    private static readonly byte[] RequireWrapper = """
        local resolve, begin, finish, has_failed, fail = ...
        local lua_error = error
        local lua_pcall = pcall
        local loaded = {}

        local function raise(message)
            if lua_error ~= nil then
                lua_error(message, 2)
            end

            return fail(message)
        end

        return function(name)
            local cached = loaded[name]
            if cached ~= nil then
                return cached
            end

            local ok, normalized_or_message = begin(name)
            if not ok then
                return raise(normalized_or_message)
            end

            name = normalized_or_message

            cached = loaded[name]
            if cached ~= nil then
                finish(name)
                return cached
            end

            local loader, message = resolve(name)
            if loader == nil then
                finish(name)
                return raise(message)
            end

            local result
            if lua_pcall ~= nil then
                local success, value = lua_pcall(loader)
                finish(name)

                if not success then
                    return raise(value)
                end

                result = value
            else
                result = loader()
                finish(name)

                if has_failed() then
                    return nil
                end
            end

            if result == nil then
                result = true
            end

            loaded[name] = result
            return result
        end
        """u8.ToArray();
    private static readonly byte[] RequireWrapperName = "poplua:require-wrapper\0"u8.ToArray();
    private static readonly object SessionsLock = new();
    private static readonly object TracebackLock = new();
    private static readonly Dictionary<nint, Session> Sessions = [];
    [ThreadStatic] private static ScriptContext? _currentContext;

    private readonly object _executionLock = new();
    private readonly Engine _runtime;
    private readonly State _lua;
    private readonly ModuleResolver? _moduleResolver;
    private readonly List<string> _loadingModules = [];
    private readonly Stopwatch _activeClock = new();
    private readonly Stopwatch _wallClock = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _disposed;
    private bool _executionActive;
    private bool _installCancellationHook;
    private long _instructions;
    private int _callDepth;
    private int _maxCallDepthObserved;
    private CancellationToken _activeCancellation;
    private RuntimeException? _managedError;
    private Task<Result>? _activeExecution;

    internal Session(Engine runtime, Sandbox sandbox, Identity identity, IServiceProvider services)
    {
        _runtime = runtime;
        Sandbox = sandbox;
        Identity = identity;
        Services = services;
        _moduleResolver = services.GetService(typeof(ModuleResolver)) as ModuleResolver ?? runtime.ModuleResolver;
        _lua = State.Create(
            libs: sandbox.Libs,
            allocator: ResolveAllocator(runtime.Allocator, sandbox));

        RegisterState(_lua.Handle.Value);
        PendingOperation.Register(_lua.Stack);
        RegisterRequire();
        RegisterModules();
    }

    /// <summary>
    /// Gets the identity assigned to this session.
    /// </summary>
    public Identity Identity { get; }

    /// <summary>
    /// Gets the sandbox policy enforced by this session.
    /// </summary>
    public Sandbox Sandbox { get; }

    /// <summary>
    /// Gets the services available to generated bindings in this session.
    /// </summary>
    public IServiceProvider Services { get; }

    internal static ScriptContext? CurrentContext => _currentContext;

    internal static void SetManagedError(nint state, Exception error)
    {
        lock (SessionsLock)
        {
            if (Sessions.TryGetValue(state, out var session))
                session._managedError ??= error as RuntimeException ?? new ScriptException(error.Message, inner: error);
        }
    }

    /// <summary>
    /// Runs Lua source text and returns the first result value or an execution error.
    /// </summary>
    /// <param name="code">Lua source text encoded as UTF-16 in the host application.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A result containing the first Lua return value, or a PopLua error.</returns>
    /// <remarks>
    /// The chunk is anonymous. Use <see cref="Run(Chunk, CancellationToken)"/>
    /// with a named chunk for user-authored scripts so errors, tracebacks, and
    /// diagnostics identify the script.
    /// </remarks>
    public ValueTask<Result> Run(string code, CancellationToken ct = default)
        => Run(Chunk.Code(code), ct);

    /// <summary>
    /// Runs Lua source text and converts the first result value to the requested type.
    /// </summary>
    /// <typeparam name="T">The expected C# result type.</typeparam>
    /// <param name="code">Lua source text encoded as UTF-16 in the host application.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A typed result containing the converted first Lua return value, or a PopLua error.</returns>
    public ValueTask<Result<T>> Run<T>(string code, CancellationToken ct = default)
        => Run<T>(Chunk.Code(code), ct);

    /// <summary>
    /// Runs a Lua chunk and returns the first result value or an execution error.
    /// </summary>
    /// <param name="chunk">Lua source chunk. Use a stable name for diagnostics when running user-authored scripts.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A result containing the first Lua return value, or a PopLua error.</returns>
    /// <remarks>
    /// Script, quota, sandbox, cancellation, and async task failures are returned
    /// in <see cref="Result.Error"/>. Host misuse, such as using a disposed
    /// session, may throw directly. A session rejects reentrant execution while a
    /// run or call is active or suspended.
    /// </remarks>
    public ValueTask<Result> Run(Chunk chunk, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return new ValueTask<Result>(StartExecution(ctx => ExecuteChunkAsync(ctx, chunk), ct));
    }

    /// <summary>
    /// Runs a Lua chunk and converts the first result value to the requested type.
    /// </summary>
    /// <typeparam name="T">The expected C# result type.</typeparam>
    /// <param name="chunk">Lua source chunk. Use a stable name for diagnostics when running user-authored scripts.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A typed result containing the converted first Lua return value, or a PopLua error.</returns>
    public async ValueTask<Result<T>> Run<T>(Chunk chunk, CancellationToken ct = default)
    {
        var result = await Run(chunk, ct).ConfigureAwait(false);
        return ToTypedResult<T>(result);
    }

    /// <summary>
    /// Runs precompiled Lua bytecode and returns the first result value or an execution error.
    /// </summary>
    /// <param name="bytecode">Bytecode produced by <see cref="Compile(Chunk)"/>.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A result containing the first Lua return value, or a PopLua error.</returns>
    /// <remarks>
    /// Bytecode can be reused across sessions in the same host process. Do not
    /// accept bytecode from untrusted users; compile trusted source text instead
    /// so the host retains source, review history, and Lua 5.4 or 5.5 compatibility control.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytecode"/> is <see langword="null"/>.</exception>
    public ValueTask<Result> Run(Bytecode bytecode, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(bytecode);

        return Run(Chunk.Utf8(bytecode.Data, bytecode.Name), ct);
    }

    /// <summary>
    /// Runs precompiled Lua bytecode and converts the first result value to the requested type.
    /// </summary>
    /// <typeparam name="T">The expected C# result type.</typeparam>
    /// <param name="bytecode">Bytecode produced by <see cref="Compile(Chunk)"/>.</param>
    /// <param name="ct">Cancellation token that terminates active execution and async bridge waits.</param>
    /// <returns>A typed result containing the converted first Lua return value, or a PopLua error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bytecode"/> is <see langword="null"/>.</exception>
    public ValueTask<Result<T>> Run<T>(Bytecode bytecode, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(bytecode);

        return Run<T>(Chunk.Utf8(bytecode.Data, bytecode.Name), ct);
    }

    /// <summary>
    /// Calls a global Lua function in this session with the provided arguments.
    /// </summary>
    /// <param name="global">Name of the global Lua function to call.</param>
    /// <param name="args">Arguments pushed to Lua before the function call.</param>
    /// <returns>A result containing the first Lua return value, or a PopLua error.</returns>
    /// <remarks>
    /// The function must already exist in the session state, for example because
    /// an earlier chunk defined it. Diagnostics use a synthetic chunk name of
    /// <c>call:&lt;global&gt;</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="global"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public ValueTask<Result> Call(string global, params Value[] args)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(global);

        return new ValueTask<Result>(StartExecution(ctx => ExecuteCallAsync(ctx, global, args), cancellation: default));
    }

    /// <summary>
    /// Calls a global Lua function and converts the first result value to the requested type.
    /// </summary>
    /// <typeparam name="T">The expected C# result type.</typeparam>
    /// <param name="global">Name of the global Lua function to call.</param>
    /// <param name="args">Arguments pushed to Lua before the function call.</param>
    /// <returns>A typed result containing the converted first Lua return value, or a PopLua error.</returns>
    public async ValueTask<Result<T>> Call<T>(string global, params Value[] args)
        => ToTypedResult<T>(await Call(global, args).ConfigureAwait(false));

    /// <summary>
    /// Compiles Lua source text to bytecode owned by the returned value.
    /// </summary>
    /// <param name="code">Lua source text encoded as UTF-16 in the host application.</param>
    /// <param name="name">Optional chunk name preserved in diagnostics and returned bytecode.</param>
    /// <returns>Compiled Lua bytecode owned by the returned <see cref="Bytecode"/>.</returns>
    /// <remarks>
    /// Compile errors are thrown as <see cref="ScriptException"/> because no
    /// execution result exists yet. Use stable names for submitted scripts.
    /// </remarks>
    /// <example>
    /// <code>
    /// var bytecode = session.Compile(scriptText, name: "plugin:on_start.lua");
    /// var result = await session.Run(bytecode);
    /// </code>
    /// </example>
    public Bytecode Compile(string code, string? name = null)
        => Compile(Chunk.Code(code, name));

    /// <summary>
    /// Compiles a Lua chunk to bytecode owned by the returned value.
    /// </summary>
    /// <param name="chunk">Lua source chunk to compile.</param>
    /// <returns>Compiled Lua bytecode owned by the returned <see cref="Bytecode"/>.</returns>
    /// <remarks>
    /// Bytecode is tied to Lua bytecode compatibility and PopLua's Lua 5.4 or 5.5
    /// runtime expectations. Store source alongside bytecode when users need
    /// editable scripts or audit history.
    /// </remarks>
    /// <exception cref="ScriptException">Thrown when Lua rejects the source or bytecode dumping fails.</exception>
    public Bytecode Compile(Chunk chunk)
    {
        ThrowIfDisposed();

        var loadError = Load(_lua.Handle.Value, chunk.Bytes.Span, chunk.Name ?? "chunk");
        if (loadError is not null)
            throw loadError;

        var bytes = new List<byte>();
        var handle = GCHandle.Alloc(bytes);

        try
        {
            int status;
            unsafe
            {
                status = NativeApi.Dump(_lua.Handle.Value, &WriteBytecode, GCHandle.ToIntPtr(handle), strip: 0);
            }

            if (status != OkStatus)
                throw new ScriptException("Lua bytecode dump failed.");
        }
        finally
        {
            handle.Free();
            _lua.Stack.Pop();
        }

        return new Bytecode(bytes.ToArray(), chunk.Name);
    }

    /// <summary>
    /// Cancels active execution if needed, waits for deterministic completion, and closes the Lua state.
    /// </summary>
    /// <returns>A task that completes after any active execution has observed disposal cancellation and the Lua state is closed.</returns>
    /// <remarks>
    /// Disposal during async suspension is terminal for the active execution.
    /// The session cannot be reused after disposal.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        Task<Result>? active;

        lock (_executionLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _disposeCts.Cancel();
            active = _activeExecution;
        }

        if (active is not null)
        {
            try
            {
                await active.ConfigureAwait(false);
            }
            catch
            {
                // Active executions complete through Result; disposal should
                // still close the state if an unexpected host exception escapes.
            }
        }

        lock (SessionsLock)
        {
            Sessions.Remove(_lua.Handle.Value);
        }

        _disposeCts.Dispose();
        _lua.Dispose();
    }

    private Task<Result> StartExecution(Func<ScriptContext, Task<Result>> execute, CancellationToken cancellation)
    {
        CancellationTokenSource linkedCts;
        var ctx = CreateContext(cancellation, out linkedCts);
        var completion = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_executionLock)
        {
            try
            {
                ThrowIfDisposed();

                if (_executionActive)
                    throw new InvalidOperationException("Session already has an active execution.");

                _executionActive = true;
                _installCancellationHook = cancellation.CanBeCanceled;
                _activeExecution = completion.Task;
            }
            catch
            {
                linkedCts.Dispose();
                throw;
            }
        }

        _ = ExecuteWithLifecycleAsync(ctx, linkedCts, execute, completion);
        return completion.Task;
    }

    private async Task ExecuteWithLifecycleAsync(
        ScriptContext ctx,
        CancellationTokenSource linkedCts,
        Func<ScriptContext, Task<Result>> execute,
        TaskCompletionSource<Result> completion)
    {
        Result result = default;
        Exception? error = null;
        try
        {
            result = await execute(ctx).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            linkedCts.Dispose();
            lock (_executionLock)
            {
                _executionActive = false;
                _installCancellationHook = false;
                _activeExecution = null;
            }
        }

        if (error is null)
            completion.TrySetResult(result);
        else
            completion.TrySetException(error);
    }

    private async Task<Result> ExecuteChunkAsync(ScriptContext ctx, Chunk chunk)
    {
        _runtime.Diagnostics.Started(ctx, chunk);

        var execution = CreateExecution(ctx, chunk.Name ?? "chunk");
        try
        {
            var loadError = Load(execution.Thread, chunk.Bytes.Span, chunk.Name ?? "chunk");
            if (loadError is not null)
                return Fail(ctx, loadError, execution);

            return await ResumeUntilCompleteAsync(execution, args: 0).ConfigureAwait(false);
        }
        finally
        {
            DisposeExecution(execution);
        }
    }

    private async Task<Result> ExecuteCallAsync(ScriptContext ctx, string global, Value[] args)
    {
        var diagnosticChunk = Chunk.Code(string.Empty, name: $"call:{global}");
        _runtime.Diagnostics.Started(ctx, diagnosticChunk);

        var execution = CreateExecution(ctx, diagnosticChunk.Name);
        try
        {
            PushGlobal(execution.Thread, global);

            var threadStack = new StateStack(new StateHandle(execution.Thread));
            if (threadStack.TypeOf(-1) != NativeType.Function)
                return Fail(ctx, new NativeTypeException("function", ValueKind.Nil), execution);

            foreach (var arg in args)
                threadStack.PushValue(arg);

            return await ResumeUntilCompleteAsync(execution, args.Length).ConfigureAwait(false);
        }
        finally
        {
            DisposeExecution(execution);
        }
    }

    internal ValueTask<Result> CallFunction(FunctionRef function, Value[] args, CancellationToken ct)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(function);
        function.ThrowIfDisposed();

        if (!ReferenceEquals(function.Session, this))
            throw new InvalidOperationException("Lua function reference belongs to another session.");

        return new ValueTask<Result>(StartExecution(ctx => ExecuteFunctionRefAsync(ctx, function.RegistryReference, args), ct));
    }

    internal void ReleaseFunction(FunctionRef function)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (!ReferenceEquals(function.Session, this))
            throw new InvalidOperationException("Lua function reference belongs to another session.");

        lock (_executionLock)
        {
            if (_disposed)
                return;

            ThrowIfActive();
            NativeApi.Unref(_lua.Handle.Value, NativeApi.RegistryIndex, function.RegistryReference);
        }
    }

    internal static FunctionRef CaptureFunction(nint state, int index)
    {
        if (TryGetSession(state) is not { } session)
            throw new ScriptException("Lua function reference capture failed: session unavailable.");

        var stack = new StateStack(new StateHandle(state));
        if (stack.TypeOf(index) != NativeType.Function)
            throw new NativeTypeException("function", ToValueKind(stack.TypeOf(index)));

        NativeApi.PushValue(state, index);
        var reference = NativeApi.Ref(state, NativeApi.RegistryIndex);
        return new FunctionRef(session, reference);
    }

    private async Task<Result> ExecuteFunctionRefAsync(ScriptContext ctx, int registryReference, Value[] args)
    {
        var diagnosticChunk = Chunk.Code(string.Empty, name: "call:function-ref");
        _runtime.Diagnostics.Started(ctx, diagnosticChunk);

        var execution = CreateExecution(ctx, diagnosticChunk.Name);
        try
        {
            NativeApi.RawGetI(execution.Thread, NativeApi.RegistryIndex, registryReference);

            var threadStack = new StateStack(new StateHandle(execution.Thread));
            if (threadStack.TypeOf(-1) != NativeType.Function)
                return Fail(ctx, new NativeTypeException("function", ToValueKind(threadStack.TypeOf(-1))), execution);

            foreach (var arg in args)
                threadStack.PushValue(arg);

            return await ResumeUntilCompleteAsync(execution, args.Length).ConfigureAwait(false);
        }
        finally
        {
            DisposeExecution(execution);
        }
    }

    private Execution CreateExecution(ScriptContext ctx, string? chunkName)
    {
        _instructions = 0;
        _callDepth = 0;
        _maxCallDepthObserved = 0;
        _managedError = null;
        _loadingModules.Clear();
        _activeCancellation = ctx.Cancellation;
        _lua.Allocator?.ResetQuotaState();
        _activeClock.Reset();
        _wallClock.Restart();

        var thread = NativeApi.NewThread(_lua.Handle.Value);
        var threadRef = NativeApi.Ref(_lua.Handle.Value, NativeApi.RegistryIndex);

        RegisterState(thread);
        InstallQuotaHook(thread);

        return new Execution(ctx, thread, threadRef, chunkName);
    }

    private void DisposeExecution(Execution execution)
    {
        _activeClock.Stop();
        _wallClock.Stop();
        _activeCancellation = default;
        UnregisterState(execution.Thread);
        NativeApi.Unref(_lua.Handle.Value, NativeApi.RegistryIndex, execution.ThreadRegistryRef);
    }

    private async Task<Result> ResumeUntilCompleteAsync(Execution execution, int args)
    {
        while (true)
        {
            var status = Resume(execution, args, out var results);
            args = 0;

            if (status == YieldStatus)
            {
                var yielded = HandleYield(execution, results);
                if (!yielded.Ok)
                    return yielded.Result;

                var waitResult = await WaitForPendingOperation(execution, yielded.Operation).ConfigureAwait(false);
                if (waitResult == AsyncWaitResult.ActiveTimeExceeded)
                {
                    PendingOperation.ReleaseToken(execution.Thread, -1);
                    return Fail(execution.Context, new QuotaException(QuotaKind.ActiveTime), execution);
                }

                if (waitResult == AsyncWaitResult.WallTimeExceeded)
                {
                    PendingOperation.ReleaseToken(execution.Thread, -1);
                    return Fail(execution.Context, new QuotaException(QuotaKind.WallTime), execution);
                }

                if (waitResult == AsyncWaitResult.Canceled)
                {
                    PendingOperation.ReleaseToken(execution.Thread, -1);
                    return Fail(execution.Context, new ScriptException("Lua execution canceled."), execution);
                }

                if (execution.Context.Cancellation.IsCancellationRequested)
                {
                    PendingOperation.ReleaseToken(execution.Thread, -1);
                    return Fail(execution.Context, new ScriptException("Lua execution canceled."), execution);
                }

                new StateStack(new StateHandle(execution.Thread)).SetTop(0);
                continue;
            }

            if (status != OkStatus)
            {
                if (_lua.Allocator?.MemoryLimitExceeded == true)
                    return Fail(execution.Context, new QuotaException(QuotaKind.Memory), execution);

                return Fail(
                    execution.Context,
                    CreateException(ReadErrorInfo(
                        execution.Thread,
                        includeTraceback: true,
                        execution.ChunkName,
                        execution.ThreadRegistryRef)),
                    execution);
            }

            if (Sandbox.MaxActiveTime is { } maxActiveTime && _activeClock.Elapsed > maxActiveTime)
                return Fail(execution.Context, new QuotaException(QuotaKind.ActiveTime), execution);

            if (Sandbox.MaxWallTime is { } maxWallTime && _wallClock.Elapsed > maxWallTime)
                return Fail(execution.Context, new QuotaException(QuotaKind.WallTime), execution);

            if (_managedError is not null)
                return Fail(execution.Context, _managedError, execution);

            var stack = new StateStack(new StateHandle(execution.Thread));
            var value = results > 0 ? stack.ReadValue(-1) : Value.Nil;
            stack.SetTop(0);

            var metrics = new Metrics(_wallClock.Elapsed, _instructions, ToLong(_lua.Allocator?.PeakBytes), _maxCallDepthObserved);
            _runtime.Diagnostics.Completed(execution.Context, in metrics);
            return Result.Success(value);
        }
    }

    private (bool Ok, Result Result, PendingOperation Operation) HandleYield(Execution execution, int results)
    {
        var stack = new StateStack(new StateHandle(execution.Thread));
        if (results != 1)
            return (false, Fail(execution.Context, new ScriptException("Lua yielded an unsupported value."), execution), null!);

        try
        {
            return (true, default, PendingOperation.Read(execution.Thread, -1));
        }
        catch (RuntimeException error)
        {
            stack.SetTop(0);
            return (false, Fail(execution.Context, error, execution), null!);
        }
    }

    private async ValueTask<AsyncWaitResult> WaitForPendingOperation(Execution execution, PendingOperation operation)
    {
        if (operation.PauseActiveTime)
            return await WaitForPendingOperationCore(execution, operation, countActiveTime: false).ConfigureAwait(false);

        _activeClock.Start();
        try
        {
            return await WaitForPendingOperationCore(execution, operation, countActiveTime: true).ConfigureAwait(false);
        }
        finally
        {
            _activeClock.Stop();
        }
    }

    private async ValueTask<AsyncWaitResult> WaitForPendingOperationCore(
        Execution execution,
        PendingOperation operation,
        bool countActiveTime)
    {
        using var activeTimeoutCts = countActiveTime && Sandbox.MaxActiveTime is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(execution.Context.Cancellation)
            : null;

        var cancellation = execution.Context.Cancellation;
        if (activeTimeoutCts is not null)
        {
            var remaining = Sandbox.MaxActiveTime!.Value - _activeClock.Elapsed;
            if (remaining <= TimeSpan.Zero)
                return AsyncWaitResult.ActiveTimeExceeded;

            activeTimeoutCts.CancelAfter(remaining);
            cancellation = activeTimeoutCts.Token;
        }

        var waited = await operation.WaitAsync(cancellation).ConfigureAwait(false);
        if (waited)
        {
            if (Sandbox.MaxActiveTime is { } activeTime && _activeClock.Elapsed > activeTime)
                return AsyncWaitResult.ActiveTimeExceeded;

            if (Sandbox.MaxWallTime is { } wallTime && _wallClock.Elapsed > wallTime)
                return AsyncWaitResult.WallTimeExceeded;

            return AsyncWaitResult.Completed;
        }

        if (Sandbox.MaxActiveTime is { } maxActiveTime && IsActiveTimeExceeded(maxActiveTime))
            return AsyncWaitResult.ActiveTimeExceeded;

        if (Sandbox.MaxWallTime is { } maxWallTime && IsWallTimeExceeded(execution.Context, maxWallTime))
            return AsyncWaitResult.WallTimeExceeded;

        return AsyncWaitResult.Canceled;
    }

    private bool IsWallTimeExceeded(ScriptContext context, TimeSpan maxWallTime)
    {
        if (_wallClock.Elapsed >= maxWallTime)
            return true;

        return context.Cancellation.IsCancellationRequested
            && maxWallTime - _wallClock.Elapsed <= TimeSpan.FromMilliseconds(50);
    }

    private bool IsActiveTimeExceeded(TimeSpan maxActiveTime)
        => _activeClock.Elapsed >= maxActiveTime
           || maxActiveTime - _activeClock.Elapsed <= TimeSpan.FromMilliseconds(50);

    private unsafe int Resume(Execution execution, int args, out int results)
    {
        var previousContext = _currentContext;
        _currentContext = execution.Context;
        _activeClock.Start();

        int status = default;
        int resultCount = default;
        try
        {
            status = NativeApi.Resume(execution.Thread, _lua.Handle.Value, args, &resultCount);
        }
        finally
        {
            _activeClock.Stop();
            _currentContext = previousContext;
        }

        results = resultCount;
        execution.Context.State.Instructions = _instructions;
        execution.Context.State.PeakMemoryBytes = ToLong(_lua.Allocator?.PeakBytes);
        execution.Context.State.CallDepth = _callDepth;
        execution.Context.State.Elapsed = _wallClock.Elapsed;
        return status;
    }

    private void RegisterModules()
    {
        foreach (var module in _runtime.Modules)
        {
            if (module.Cap is not null && !Sandbox.Has(module.Cap))
                continue;

            module.Register(new Registration(_lua.Stack));
        }
    }

    private unsafe void RegisterRequire()
    {
        if (_moduleResolver is null)
            return;

        fixed (byte* wrapperPtr = RequireWrapper)
        fixed (byte* namePtr = RequireWrapperName)
        {
            var status = NativeApi.LoadBuffer(_lua.Handle.Value, wrapperPtr, (nuint)RequireWrapper.Length, namePtr, null);
            if (status != OkStatus)
                throw new ScriptException(ReadRegistrationError());
        }

        NativeApi.PushCClosure(_lua.Handle.Value, &RequireResolveCallback, 0);
        NativeApi.PushCClosure(_lua.Handle.Value, &RequireBeginCallback, 0);
        NativeApi.PushCClosure(_lua.Handle.Value, &RequireFinishCallback, 0);
        NativeApi.PushCClosure(_lua.Handle.Value, &RequireHasFailedCallback, 0);
        NativeApi.PushCClosure(_lua.Handle.Value, &RequireFailCallback, 0);

        var callStatus = NativeApi.PCall(_lua.Handle.Value, 5, 1, errorFunction: 0, context: 0, continuation: 0);
        if (callStatus != OkStatus)
            throw new ScriptException(ReadRegistrationError());

        SetGlobal(_lua.Handle.Value, "require");
    }

    private void InstallQuotaHook(nint state)
    {
        var mask = HookMask();
        if (mask == 0)
            return;

        unsafe
        {
            NativeApi.SetHook(state, &QuotaHook, mask, Sandbox.HookInterval);
        }
    }

    private int HookMask()
    {
        var mask = 0;

        if (Sandbox.MaxInstructions is not null
            || Sandbox.MaxActiveTime is not null
            || Sandbox.MaxWallTime is not null
            || _installCancellationHook
            || _lua.Allocator?.GcThresholdBytes > 0)
        {
            mask |= MaskCount;
        }

        if (Sandbox.MaxCallDepth is not null)
            mask |= MaskCall | MaskReturn;

        return mask;
    }

    private ScriptContext CreateContext(CancellationToken cancellation, out CancellationTokenSource linkedCts)
    {
        linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeCts.Token);
        if (Sandbox.MaxWallTime is { } maxWallTime)
            linkedCts.CancelAfter(maxWallTime);

        return ScriptContext.Create(Sandbox, Identity, Services, linkedCts.Token);
    }

    private unsafe RuntimeException? Load(nint state, ReadOnlySpan<byte> bytes, string name)
    {
        var luaName = ChunkName(name);
        var nameByteCount = Encoding.UTF8.GetByteCount(luaName);
        var nameBytes = new byte[nameByteCount + 1];
        Encoding.UTF8.GetBytes(luaName, 0, luaName.Length, nameBytes, 0);

        fixed (byte* chunkPtr = bytes)
        fixed (byte* namePtr = nameBytes)
        {
            var status = NativeApi.LoadBuffer(state, chunkPtr, (nuint)bytes.Length, namePtr, null);
            if (status == OkStatus)
                return null;
        }

        if (_lua.Allocator?.MemoryLimitExceeded == true)
            return new QuotaException(QuotaKind.Memory);

        var info = ReadErrorInfo(state, includeTraceback: false, fallbackChunk: name);
        return CreateScriptException(info);
    }

    private static string ChunkName(string name)
        => name.Length > 0 && (name[0] == '@' || name[0] == '=')
            ? name
            : "@" + name;

    private Result Fail(ScriptContext ctx, RuntimeException error, Execution execution)
    {
        error = Enrich(error, execution);
        new StateStack(new StateHandle(execution.Thread)).SetTop(0);

        if (error is QuotaException quota)
            _runtime.Diagnostics.QuotaBlocked(ctx, quota.Kind);

        if (error is SandboxException sandbox)
            _runtime.Diagnostics.SandboxBlocked(ctx, sandbox.Cap);

        _runtime.Diagnostics.Failed(ctx, error);
        return Result.Failure(error);
    }

    private RuntimeException Enrich(RuntimeException error, Execution execution)
    {
        if (error is not ScriptException script || script.Chunk is not null || execution.ChunkName is null)
            return error;

        return new ScriptException(script.Message, script.LuaTrace, script.InnerException)
        {
            Chunk = execution.ChunkName,
            Line = script.Line,
        };
    }

    private ErrorInfo ReadErrorInfo(nint state, bool includeTraceback, string? fallbackChunk, int threadRegistryRef = 0)
    {
        var stack = new StateStack(new StateHandle(state));
        var message = stack.TypeOf(-1) == NativeType.String
            ? stack.ReadString(-1)
            : "Lua execution failed.";
        var trace = includeTraceback && _lua.Allocator is null
            ? ReadTraceback(message, threadRegistryRef)
            : null;

        if (stack.Top > 0)
            stack.Pop();

        var location = ParseLocation(message);
        return new ErrorInfo(
            message,
            trace,
            location.Chunk ?? fallbackChunk,
            location.Line);
    }

    private unsafe string? ReadTraceback(string message, int threadRegistryRef)
    {
        if (threadRegistryRef == 0)
            return null;

        var mainStack = _lua.Stack;
        var top = mainStack.Top;

        try
        {
            lock (TracebackLock)
            {
                NativeApi.OpenDebug(mainStack.State.Value);

                fixed (byte* fieldPtr = TracebackField)
                    NativeApi.GetField(mainStack.State.Value, -1, fieldPtr);

                mainStack.Remove(-2);
                NativeApi.RawGetI(mainStack.State.Value, NativeApi.RegistryIndex, threadRegistryRef);
                mainStack.PushString(message);
                mainStack.PushInteger(0);

                var status = NativeApi.PCall(mainStack.State.Value, 3, 1, errorFunction: 0, context: 0, continuation: 0);
                if (status != OkStatus)
                    return null;
            }

            return mainStack.TypeOf(-1) == NativeType.String
                ? mainStack.ReadString(-1)
                : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            mainStack.SetTop(top);
        }
    }

    private static (string? Chunk, int? Line) ParseLocation(string message)
    {
        for (var i = 0; i < message.Length; i++)
        {
            if (message[i] != ':' || i == 0)
                continue;

            var digitStart = i + 1;
            var digitEnd = digitStart;
            while (digitEnd < message.Length && char.IsAsciiDigit(message[digitEnd]))
                digitEnd++;

            if (digitEnd == digitStart || digitEnd >= message.Length || message[digitEnd] != ':')
                continue;

            if (int.TryParse(message.AsSpan(digitStart, digitEnd - digitStart), out var line))
                return (message[..i], line);
        }

        return (null, null);
    }

    private RuntimeException CreateException(ErrorInfo error)
    {
        return error.Message switch
        {
            "poplua:quota:instructions" => new QuotaException(QuotaKind.Instructions),
            "poplua:quota:active-time" => new QuotaException(QuotaKind.ActiveTime),
            "poplua:quota:wall-time" => new QuotaException(QuotaKind.WallTime),
            "poplua:quota:memory" => new QuotaException(QuotaKind.Memory),
            "poplua:quota:call-depth" => new QuotaException(QuotaKind.CallDepth),
            "poplua:canceled" => new ScriptException("Lua execution canceled."),
            _ => CreateScriptException(error),
        };
    }

    private static ScriptException CreateScriptException(ErrorInfo error)
        => new(error.Message, error.Trace)
        {
            Chunk = error.Chunk,
            Line = error.Line,
        };

    internal static Result<T> ToTypedResult<T>(Result result)
    {
        if (!result.Ok)
            return Result<T>.Failure(result.Error!);

        try
        {
            return Result<T>.Success(ConvertValue<T>(result.Value));
        }
        catch (RuntimeException error)
        {
            return Result<T>.Failure(error);
        }
    }

    private static T ConvertValue<T>(Value value)
    {
        if (typeof(T) == typeof(Value))
            return (T)(object)value;

        if (typeof(T) == typeof(long))
            return (T)(object)value.Int();

        if (typeof(T) == typeof(int))
            return (T)(object)checked((int)value.Int());

        if (typeof(T) == typeof(double))
            return (T)(object)value.Number();

        if (typeof(T) == typeof(float))
            return (T)(object)checked((float)value.Number());

        if (typeof(T) == typeof(bool))
            return (T)(object)value.Bool();

        if (typeof(T) == typeof(string))
            return (T)(object)value.String();

        throw new NativeTypeException(typeof(T).Name, value.Kind);
    }

    private string ReadRegistrationError()
    {
        var stack = _lua.Stack;
        var message = stack.TypeOf(-1) == NativeType.String
            ? stack.ReadString(-1)
            : "Lua registration failed.";

        if (stack.Top > 0)
            stack.Pop();

        return message;
    }

    private static Session? TryGetSession(nint state)
    {
        lock (SessionsLock)
        {
            Sessions.TryGetValue(state, out var session);
            return session;
        }
    }

    private static bool TryNormalizeModuleName(string name, out string normalized, out string message)
    {
        normalized = string.Empty;
        message = string.Empty;

        if (name.Length == 0)
        {
            message = InvalidModuleNameMessage(name);
            return false;
        }

        if (name.Length > 128)
        {
            message = InvalidModuleNameMessage(name);
            return false;
        }

        if (name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
        {
            message = InvalidModuleNameMessage(name);
            return false;
        }

        var segmentStart = 0;
        var previousWasDot = false;
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsControl(c) || c == '/' || c == '\\' || c == ':')
            {
                message = InvalidModuleNameMessage(name);
                return false;
            }

            if (c == '.')
            {
                if (i == segmentStart || previousWasDot)
                {
                    message = InvalidModuleNameMessage(name);
                    return false;
                }

                segmentStart = i + 1;
                previousWasDot = true;
                continue;
            }

            var offset = i - segmentStart;
            var valid = offset == 0
                ? char.IsAsciiLetter(c) || c == '_'
                : char.IsAsciiLetterOrDigit(c) || c == '_';

            if (!valid)
            {
                message = InvalidModuleNameMessage(name);
                return false;
            }

            previousWasDot = false;
        }

        if (previousWasDot)
        {
            message = InvalidModuleNameMessage(name);
            return false;
        }

        normalized = name;
        return true;
    }

    private static string InvalidModuleNameMessage(string name)
        => name.Length == 0
            ? "invalid module name: <empty>"
            : name.Length > 128
                ? "invalid module name: too long"
                : "invalid module name: " + name;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int RequireBeginCallback(nint state)
    {
        try
        {
            var stack = new StateStack(new StateHandle(state));
            if (TryGetSession(state) is not { } session)
                return PushRequireFailure(stack, "module load failed: session unavailable");

            if (stack.TypeOf(1) != NativeType.String)
                return PushRequireFailure(stack, "invalid module name: expected string");

            var requested = stack.ReadString(1);
            if (!TryNormalizeModuleName(requested, out var normalized, out var message))
                return PushRequireFailure(stack, message);

            var existingIndex = session._loadingModules.IndexOf(normalized);
            if (existingIndex >= 0)
                return PushRequireFailure(stack, "cyclic module load: " + session.FormatModuleLoadCycle(existingIndex, normalized));

            session._loadingModules.Add(normalized);

            stack.PushBoolean(true);
            stack.PushString(normalized);
            return 2;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int RequireResolveCallback(nint state)
    {
        try
        {
            var stack = new StateStack(new StateHandle(state));
            if (TryGetSession(state) is not { } session)
                return PushRequireMissing(stack, "module load failed: session unavailable");

            if (session._moduleResolver is null)
                return PushRequireMissing(stack, "module load failed: resolver not configured");

            if (stack.TypeOf(1) != NativeType.String)
                return PushRequireMissing(stack, "invalid module name: expected string");

            var moduleName = stack.ReadString(1);
            Chunk? resolved;
            try
            {
                resolved = session._moduleResolver(Session.CurrentContext ?? ScriptContext.Create(), moduleName);
            }
            catch (Exception ex)
            {
                return PushRequireMissing(stack, $"module resolver failed: {moduleName}: {ex.Message}");
            }

            if (resolved is not { } chunk)
                return PushRequireMissing(stack, $"module not found: {moduleName}");

            var loadError = session.Load(state, chunk.Bytes.Span, chunk.Name ?? $"module:{moduleName}.lua");
            if (loadError is not null)
                return PushRequireMissing(stack, loadError.Message);

            return 1;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int RequireFinishCallback(nint state)
    {
        try
        {
            if (TryGetSession(state) is { } session)
            {
                var stack = new StateStack(new StateHandle(state));
                if (stack.TypeOf(1) == NativeType.String)
                    session.FinishModuleLoad(stack.ReadString(1));
            }

            return 0;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int RequireHasFailedCallback(nint state)
    {
        try
        {
            var stack = new StateStack(new StateHandle(state));
            stack.PushBoolean(TryGetSession(state)?._managedError is not null);
            return 1;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int RequireFailCallback(nint state)
    {
        try
        {
            var stack = new StateStack(new StateHandle(state));
            var message = stack.TypeOf(1) == NativeType.String
                ? stack.ReadString(1)
                : "module load failed";

            Session.SetManagedError(state, new ScriptException(message));
            return 0;
        }
        catch (Exception ex)
        {
            return Marshaller.Error(state, ex);
        }
    }

    private static int PushRequireFailure(StateStack stack, string message)
    {
        stack.PushBoolean(false);
        stack.PushString(message);
        return 2;
    }

    private static int PushRequireMissing(StateStack stack, string message)
    {
        stack.PushNil();
        stack.PushString(message);
        return 2;
    }

    private string FormatModuleLoadCycle(int startIndex, string repeated)
    {
        var builder = new StringBuilder();
        for (var i = startIndex; i < _loadingModules.Count; i++)
        {
            if (builder.Length > 0)
                builder.Append(" -> ");

            builder.Append(_loadingModules[i]);
        }

        if (builder.Length > 0)
            builder.Append(" -> ");

        builder.Append(repeated);
        return builder.ToString();
    }

    private void FinishModuleLoad(string name)
    {
        for (var i = _loadingModules.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_loadingModules[i], name, StringComparison.Ordinal))
            {
                _loadingModules.RemoveAt(i);
                return;
            }
        }
    }

    private unsafe void PushGlobal(nint state, string name)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(name.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = Encoding.UTF8.GetBytes(name.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.GetGlobal(state, ptr);
    }

    private unsafe void SetGlobal(nint state, string name)
    {
        var maxBytes = Encoding.UTF8.GetMaxByteCount(name.Length);
        Span<byte> buffer = maxBytes + 1 <= 256 ? stackalloc byte[maxBytes + 1] : new byte[maxBytes + 1];
        var written = Encoding.UTF8.GetBytes(name.AsSpan(), buffer);
        buffer[written] = 0;

        fixed (byte* ptr = buffer[..(written + 1)])
            NativeApi.SetGlobal(state, ptr);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Session));
    }

    private void ThrowIfActive()
    {
        if (_executionActive)
            throw new InvalidOperationException("Session already has an active execution.");
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

    private void OnQuotaHook(nint state, int hookEvent)
    {
        if (_disposeCts.IsCancellationRequested)
            RaiseQuota(state, ExecutionCanceled);

        if (_activeCancellation.IsCancellationRequested)
        {
            if (Sandbox.MaxWallTime is { } canceledWallTime && IsWallTimeExceeded(canceledWallTime))
                RaiseQuota(state, QuotaWallTime);

            RaiseQuota(state, ExecutionCanceled);
        }

        if (hookEvent == HookCall)
        {
            _callDepth++;
            if (_callDepth > _maxCallDepthObserved)
                _maxCallDepthObserved = _callDepth;

            UpdateExecutionState();

            if (Sandbox.MaxCallDepth is { } maxCallDepth && _callDepth > maxCallDepth)
                RaiseQuota(state, QuotaCallDepth);

            return;
        }

        if (hookEvent == HookReturn)
        {
            if (_callDepth > 0)
                _callDepth--;

            UpdateExecutionState();
            return;
        }

        if (hookEvent != HookCount)
            return;

        _instructions += Sandbox.HookInterval;
        UpdateExecutionState();

        if (Sandbox.MaxInstructions is { } maxInstructions && _instructions > maxInstructions)
            RaiseQuota(state, QuotaInstructions);

        if (Sandbox.MaxActiveTime is { } maxActiveTime && _activeClock.Elapsed > maxActiveTime)
            RaiseQuota(state, QuotaActiveTime);

        if (Sandbox.MaxWallTime is { } maxWallTime && _wallClock.Elapsed > maxWallTime)
            RaiseQuota(state, QuotaWallTime);

        if (_lua.Allocator?.ConsumeGcRequest() == true)
        {
            NativeApi.Gc(state, GcCollect);
            if (_lua.Allocator.MemoryLimitExceeded)
                RaiseQuota(state, QuotaMemory);
        }
    }

    private void UpdateExecutionState()
    {
        var state = _currentContext?.State;
        if (state is null)
            return;

        state.Instructions = _instructions;
        state.PeakMemoryBytes = ToLong(_lua.Allocator?.PeakBytes);
        state.CallDepth = _callDepth;
        state.Elapsed = _wallClock.Elapsed;
    }

    private bool IsWallTimeExceeded(TimeSpan maxWallTime)
        => _wallClock.Elapsed >= maxWallTime
           || maxWallTime - _wallClock.Elapsed <= TimeSpan.FromMilliseconds(50);

    private static unsafe void RaiseQuota(nint state, byte[] message)
    {
        fixed (byte* ptr = message)
        {
            NativeApi.PushString(state, ptr, (nuint)message.Length);
            NativeApi.Error(state);
        }
    }

    private void RegisterState(nint state)
    {
        lock (SessionsLock)
        {
            Sessions[state] = this;
        }
    }

    private static void UnregisterState(nint state)
    {
        lock (SessionsLock)
        {
            Sessions.Remove(state);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static unsafe void QuotaHook(nint state, nint debug)
    {
        Session? session;

        lock (SessionsLock)
        {
            Sessions.TryGetValue(state, out session);
        }

        session?.OnQuotaHook(state, debug == 0 ? HookCount : *(int*)debug);
    }

    private static AllocatorOptions ResolveAllocator(AllocatorOptions runtimeAllocator, Sandbox sandbox)
    {
        var maxHeapBytes = sandbox.MaxHeapBytes ?? runtimeAllocator.MaxHeapBytes;
        var gcThresholdBytes = sandbox.GcThresholdBytes ?? runtimeAllocator.GcThresholdBytes;

        return runtimeAllocator with
        {
            MaxHeapBytes = maxHeapBytes,
            GcThresholdBytes = gcThresholdBytes,
        };
    }

    private static long ToLong(nuint? value)
        => value is null ? 0 : checked((long)value.Value);

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static unsafe int WriteBytecode(nint state, nint data, nuint size, nint userData)
    {
        if (GCHandle.FromIntPtr(userData).Target is not List<byte> bytes)
            return 1;

        bytes.AddRange(new ReadOnlySpan<byte>((void*)data, checked((int)size)).ToArray());
        return 0;
    }

    private sealed class Execution
    {
        internal Execution(ScriptContext context, nint thread, int threadRegistryRef, string? chunkName)
        {
            Context = context;
            Thread = thread;
            ThreadRegistryRef = threadRegistryRef;
            ChunkName = chunkName;
        }

        internal ScriptContext Context { get; }
        internal nint Thread { get; }
        internal int ThreadRegistryRef { get; }
        internal string? ChunkName { get; }
    }

    private enum AsyncWaitResult
    {
        Completed,
        Canceled,
        ActiveTimeExceeded,
        WallTimeExceeded,
    }

    private readonly record struct ErrorInfo(string Message, string? Trace, string? Chunk, int? Line);
}

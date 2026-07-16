namespace PopLua.Tests;

public sealed class AsyncModuleTests
{
    [Fact]
    public async Task AsyncModuleFunctionReturnsValue()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_api.delayed('ok')").AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        service.Complete("value:ok");

        var result = await run;

        Assert.True(result.Ok);
        Assert.Equal("value:ok", result.Unwrap());
    }

    [Fact]
    public async Task AsyncModuleFunctionCompletedSynchronouslyReturnsValue()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<string>("return async_api.immediate('ok')");

        Assert.True(result.Ok);
        Assert.Equal("immediate:ok", result.Unwrap());
    }

    [Fact]
    public async Task AsyncTaskFaultReturnsResultFailure()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run("return async_api.delayed('bad')").AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        service.Fail(new InvalidOperationException("async boom"));

        var result = await run;

        Assert.False(result.Ok);
        Assert.Contains("async boom", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LuaPCallCanCatchAsyncTaskFault()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<bool>("""
            local ok, err = pcall(function()
                return async_api.delayed('bad')
            end)
            return ok == false and string.find(err, 'async boom') ~= nil
            """).AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        service.Fail(new InvalidOperationException("async boom"));

        var result = await run;

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task LuaPCallCanCatchCompletedAsyncTaskFault()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<bool>("""
            local ok, err = pcall(function()
                return async_api.immediate_fault()
            end)
            return ok == false and string.find(err, 'immediate boom') ~= nil
            """);

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task CancellationDuringSuspensionFailsExecution()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);
        using var cts = new CancellationTokenSource();

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_api.delayed('wait')", cts.Token).AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel();

        var result = await run;

        Assert.False(result.Ok);
        Assert.IsType<ScriptException>(result.Error);
        Assert.Contains("canceled", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DefaultPauseTimeDoesNotConsumeActiveTimeWhileSuspended()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);
        var sandbox = Sandbox.Build(b => b.Quota(
            activeTime: TimeSpan.FromMilliseconds(20),
            wallTime: TimeSpan.FromSeconds(2),
            hookInterval: 100));

        await using var session = lua.Session(sandbox, services: services);
        var run = session.Run<string>("return async_api.delayed('pause')").AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(TimeSpan.FromMilliseconds(80));
        service.Complete("done");

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.Ok);
        Assert.Equal("pause:done", result.Unwrap());
    }

    [Fact]
    public async Task PauseTimeFalseConsumesActiveTimeWhileSuspended()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);
        var sandbox = Sandbox.Build(b => b.Quota(
            activeTime: TimeSpan.FromMilliseconds(30),
            wallTime: TimeSpan.FromSeconds(2),
            hookInterval: 100));

        await using var session = lua.Session(sandbox, services: services);
        var run = session.Run<string>("return async_api.counted_delay('work')").AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2));

        var error = Assert.IsType<QuotaException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal(QuotaKind.ActiveTime, error.Kind);
    }

    [Fact]
    public async Task WallTimeQuotaStopsSuspendedAsync()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);
        var sandbox = Sandbox.Build(b => b.Quota(
            activeTime: TimeSpan.FromSeconds(2),
            wallTime: TimeSpan.FromMilliseconds(30),
            hookInterval: 100));

        await using var session = lua.Session(sandbox, services: services);
        var run = session.Run<string>("return async_api.delayed('wait')").AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2));

        var error = Assert.IsType<QuotaException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal(QuotaKind.WallTime, error.Kind);
    }

    [Fact]
    public async Task SessionDisposalDuringSuspensionCancelsExecution()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_api.delayed('wait')").AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await session.DisposeAsync();

        var result = await run;

        Assert.False(result.Ok);
        Assert.IsType<ScriptException>(result.Error);
    }

    [Fact]
    public async Task SameSessionRunDuringSuspensionThrows()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_api.delayed('wait')").AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Run("return 1"));

        service.Complete("done");
        Assert.True((await run).Ok);
    }

    [Fact]
    public async Task InstructionQuotaStillAppliesAfterAsyncResume()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);
        var sandbox = Sandbox.Build(b => b.Quota(instructions: 1_000, hookInterval: 100));

        await using var session = lua.Session(sandbox, services: services);
        var run = session.Run("""
            async_api.delayed('wait')
            while true do end
            """).AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        service.Complete("done");

        var result = await run;

        Assert.False(result.Ok);
        Assert.IsType<QuotaException>(result.Error);
    }

    [Fact]
    public async Task CallDepthQuotaStillAppliesAfterAsyncResume()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);
        var sandbox = Sandbox.Build(b => b.Quota(callDepth: 8));

        await using var session = lua.Session(sandbox, services: services);
        var run = session.Run("""
            async_api.delayed('wait')

            local function recurse(n)
                if n == 0 then
                    return 1
                end

                local value = recurse(n - 1)
                return value
            end

            return recurse(100)
            """).AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Complete("done");

        var result = await run;

        var error = Assert.IsType<QuotaException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal(QuotaKind.CallDepth, error.Kind);
    }

    [Fact]
    public async Task DiagnosticsCompleteAfterAsyncResume()
    {
        var service = new AsyncApiService();
        var diagnostics = new RecordingDiagnostics();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>().Diagnostics(diagnostics));
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_api.delayed('ok')").AsTask();

        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        service.Complete("done");

        var result = await run;

        Assert.True(result.Ok);
        Assert.Equal(1, diagnostics.StartedCount);
        Assert.Equal(1, diagnostics.CompletedCount);
        Assert.Equal(0, diagnostics.FailedCount);
    }

    [Fact]
    public async Task MultipleAsyncCallsCanRunInOneScript()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_api.delayed('a') .. ',' .. async_api.delayed('b')").AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Complete("one");
        await service.WaitForStartedCountAsync(2);
        service.Complete("two");

        var result = await run;

        Assert.True(result.Ok);
        Assert.Equal("a:one,b:two", result.Unwrap());
    }

    [Fact]
    public async Task AsyncCallInsideLuaFunctionWorksThroughSessionCall()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var define = await session.Run("function fetch_value() return async_api.delayed('call') end");
        Assert.True(define.Ok);

        var call = session.Call<string>("fetch_value").AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Complete("done");

        var result = await call;

        Assert.True(result.Ok);
        Assert.Equal("call:done", result.Unwrap());
    }

    [Fact]
    public async Task AsyncFailureDoesNotPoisonLaterRunOnSameSession()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var failedRun = session.Run("return async_api.delayed('bad')").AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Fail(new InvalidOperationException("first failed"));

        var failed = await failedRun;
        var succeededRun = session.Run<string>("return async_api.delayed('good')").AsTask();

        await service.WaitForStartedCountAsync(2);
        service.Complete("done");

        var succeeded = await succeededRun;

        Assert.False(failed.Ok);
        Assert.Contains("first failed", failed.Error!.Message, StringComparison.Ordinal);
        Assert.True(succeeded.Ok);
        Assert.Equal("good:done", succeeded.Unwrap());
    }

    [Fact]
    public async Task CancellationDoesNotPoisonLaterRunOnSameSession()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);
        using var cts = new CancellationTokenSource();

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var canceledRun = session.Run("return async_api.delayed('cancel')", cts.Token).AsTask();

        await service.WaitForStartedCountAsync(1);
        cts.Cancel();

        var canceled = await canceledRun;
        var succeededRun = session.Run<string>("return async_api.delayed('good')").AsTask();

        await service.WaitForStartedCountAsync(2);
        service.Complete("done");

        var succeeded = await succeededRun;

        Assert.False(canceled.Ok);
        Assert.True(succeeded.Ok);
        Assert.Equal("good:done", succeeded.Unwrap());
    }

    [Fact]
    public async Task LuaPCallDoesNotCatchAsyncCancellation()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);
        using var cts = new CancellationTokenSource();

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run("""
            local ok = pcall(function()
                return async_api.delayed('cancel')
            end)
            return ok
            """, cts.Token).AsTask();

        await service.WaitForStartedCountAsync(1);
        cts.Cancel();

        var result = await run;

        Assert.False(result.Ok);
        Assert.Contains("canceled", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisposalDuringAsyncCompletionIsDeterministic()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_api.delayed('dispose')").AsTask();

        await service.WaitForStartedCountAsync(1);
        var dispose = session.DisposeAsync().AsTask();
        service.TryComplete("done");

        await dispose.WaitAsync(TimeSpan.FromSeconds(2));
        var result = await run.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(result.Ok);
        Assert.IsType<ScriptException>(result.Error);
    }

    [Fact]
    public async Task AsyncVoidAndValueTaskMethodCompletes()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<long>("async_api.mark(); return async_api.mark_count()");

        Assert.True(result.Ok);
        Assert.Equal(1, result.Unwrap());
    }

    [Fact]
    public async Task CompletedAsyncVoidReturnsNoValues()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<bool>("""
            local count = select('#', async_api.mark())
            return count == 0 and async_api.mark_count() == 1
            """);

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task AsyncModuleReturnsSupportedPrimitiveTypes()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<bool>("""
            return async_api.bool_value()
                and async_api.int_value() == -3
                and async_api.uint_value() == 4
                and async_api.long_value() == -5
                and async_api.ulong_value() == 6
                and async_api.float_value() == 1.5
                and async_api.double_value() == 2.5
                and async_api.string_value() == 'text'
            """);

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task AsyncModuleReturnsUserdataValueAndMultipleValues()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<bool>("""
            local vec = async_api.make_vec()
            local left, right = async_api.value_array()
            return vec:length() == 5
                and async_api.lua_value() == 'lua:value'
                and left == 7
                and right == 'second'
            """);

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task AsyncModuleReceivesContextAndInjectedService()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, Identity.Create("id-1", "Lan"), services);
        var result = await session.Run<string>("return async_api.context_name() .. ':' .. async_api.service_value()");

        Assert.True(result.Ok);
        Assert.Equal("Lan:service:0", result.Unwrap());
    }

    [Fact]
    public async Task AsyncModuleHiddenWhenCapabilityIsMissing()
    {
        var service = new AsyncApiService();
        var lua = Engine.Create(b => b.Modules<AsyncApiModule, AsyncSecretModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Untrusted, services: services);
        var result = await session.Run<bool>("return async_secret == nil");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }
}

[Module("async_api")]
public partial class AsyncApiModule(AsyncApiService service)
{
    [Fn("delayed", Async = true)]
    public ValueTask<string> Delayed([Context] ScriptContext ctx, string value)
        => service.Delayed(value, ctx.Cancellation);

    [Fn("counted_delay", Async = true, PauseTime = false)]
    public ValueTask<string> CountedDelay([Context] ScriptContext ctx, string value)
        => service.Delayed(value, ctx.Cancellation);

    [Fn("immediate", Async = true)]
    public ValueTask<string> Immediate(string value)
        => ValueTask.FromResult("immediate:" + value);

    [Fn("immediate_fault", Async = true)]
    public ValueTask<string> ImmediateFault()
        => new(Task.FromException<string>(new InvalidOperationException("immediate boom")));

    [Fn("mark", Async = true)]
    public ValueTask Mark()
    {
        service.Mark();
        return ValueTask.CompletedTask;
    }

    [Fn("mark_count")]
    public long MarkCount() => service.MarkCount;

    [Fn("bool_value", Async = true)]
    public ValueTask<bool> BoolValue() => ValueTask.FromResult(true);

    [Fn("int_value", Async = true)]
    public ValueTask<int> IntValue() => ValueTask.FromResult(-3);

    [Fn("uint_value", Async = true)]
    public ValueTask<uint> UIntValue() => ValueTask.FromResult(4u);

    [Fn("long_value", Async = true)]
    public ValueTask<long> LongValue() => ValueTask.FromResult(-5L);

    [Fn("ulong_value", Async = true)]
    public ValueTask<ulong> ULongValue() => ValueTask.FromResult(6UL);

    [Fn("float_value", Async = true)]
    public ValueTask<float> FloatValue() => ValueTask.FromResult(1.5f);

    [Fn("double_value", Async = true)]
    public ValueTask<double> DoubleValue() => ValueTask.FromResult(2.5);

    [Fn("string_value", Async = true)]
    public ValueTask<string> StringValue() => ValueTask.FromResult("text");

    [Fn("make_vec", Async = true)]
    public ValueTask<Vec2> MakeVec() => ValueTask.FromResult(new Vec2(3, 4));

    [Fn("lua_value", Async = true)]
    public ValueTask<Value> ValueResult() => ValueTask.FromResult(Value.From("lua:value"));

    [Fn("value_array", Async = true)]
    public ValueTask<Value[]> ValueArray()
        => ValueTask.FromResult<Value[]>([Value.From(7), Value.From("second")]);

    [Fn("context_name", Async = true)]
    public ValueTask<string> ContextName([Context] ScriptContext ctx)
        => ValueTask.FromResult(ctx.Identity.Name ?? ctx.Identity.Id);

    [Fn("service_value", Async = true)]
    public ValueTask<string> ServiceValue()
        => ValueTask.FromResult("service:" + service.MarkCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

public sealed class AsyncApiService
{
    private readonly Queue<TaskCompletionSource<string>> _pending = [];
    private readonly object _gate = new();
    private int _startedCount;

    public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int MarkCount { get; private set; }

    public async ValueTask<string> Delayed(string value, CancellationToken cancellation)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _pending.Enqueue(completion);
            _startedCount++;
        }

        Started.TrySetResult(true);
        try
        {
            var result = await completion.Task.WaitAsync(cancellation);
            return result == "value:ok" ? result : value + ":" + result;
        }
        catch (OperationCanceledException)
        {
            completion.TrySetCanceled();
            throw;
        }
    }

    public async Task WaitForStartedCountAsync(int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!timeout.IsCancellationRequested)
        {
            lock (_gate)
            {
                if (_startedCount >= count)
                    return;
            }

            await Task.Delay(10, timeout.Token);
        }

        throw new TimeoutException("Timed out waiting for async call to start.");
    }

    public void Complete(string value)
    {
        if (!TryComplete(value))
            throw new InvalidOperationException("No pending async operation.");
    }

    public bool TryComplete(string value)
    {
        while (TryDequeue(out var completion))
        {
            if (completion.TrySetResult(value))
                return true;
        }

        return false;
    }

    public void Fail(Exception error)
    {
        while (TryDequeue(out var completion))
        {
            if (completion.TrySetException(error))
                return;
        }

        throw new InvalidOperationException("No pending async operation.");
    }

    public void Mark() => MarkCount++;

    private bool TryDequeue(out TaskCompletionSource<string> completion)
    {
        lock (_gate)
            return _pending.TryDequeue(out completion!);
    }
}

[Module("async_secret", Cap = Caps.FileRead)]
public partial class AsyncSecretModule
{
    [Fn("value", Async = true)]
    public static ValueTask<long> Value() => ValueTask.FromResult(42L);
}

public sealed class RecordingDiagnostics : IDiagnostics
{
    public int StartedCount { get; private set; }
    public int CompletedCount { get; private set; }
    public int FailedCount { get; private set; }
    public int QuotaBlockedCount { get; private set; }
    public QuotaKind? LastQuotaKind { get; private set; }
    public Metrics? LastCompletedMetrics { get; private set; }

    public void Started(ScriptContext ctx, Chunk chunk)
        => StartedCount++;

    public void Completed(ScriptContext ctx, in Metrics metrics)
    {
        CompletedCount++;
        LastCompletedMetrics = metrics;
    }

    public void Failed(ScriptContext ctx, RuntimeException error)
        => FailedCount++;

    public void QuotaBlocked(ScriptContext ctx, QuotaKind kind)
    {
        QuotaBlockedCount++;
        LastQuotaKind = kind;
    }

    public void SandboxBlocked(ScriptContext ctx, string cap)
    {
    }
}

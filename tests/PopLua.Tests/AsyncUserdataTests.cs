namespace PopLua.Tests;

public sealed class AsyncUserdataTests
{
    [Fact]
    public async Task AsyncUserdataMethodReturnsValue()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_user.new('Suri'):delayed_name()").AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Complete("done");

        var result = await run;

        Assert.True(result.Ok);
        Assert.Equal("Suri:done", result.Unwrap());
    }

    [Fact]
    public async Task AsyncUserdataMethodCompletedSynchronouslyReturnsValue()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<string>("return async_user.new('Suri'):immediate_name()");

        Assert.True(result.Ok);
        Assert.Equal("immediate:Suri", result.Unwrap());
    }

    [Fact]
    public async Task AsyncUserdataVoidMethodCompletes()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<long>("async_user.new('Suri'):save(); return async_user.save_count()").AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Complete("saved");

        var result = await run;

        Assert.True(result.Ok);
        Assert.Equal(1, result.Unwrap());
    }

    [Fact]
    public async Task AsyncUserdataMethodFaultIsCaughtByPcall()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);
        var sandbox = Sandbox.Build(b => b.AllowSafeLibs());

        await using var session = lua.Session(sandbox, services: services);
        var run = session.Run<bool>("""
            local ok, err = pcall(function()
                return async_user.new('Suri'):delayed_name()
            end)
            return ok == false and string.find(err, 'userdata boom') ~= nil
            """).AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Fail(new InvalidOperationException("userdata boom"));

        var result = await run;

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task AsyncUserdataMethodFaultFailsResult()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run("return async_user.new('Suri'):delayed_name()").AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Fail(new InvalidOperationException("userdata boom"));

        var result = await run;

        Assert.False(result.Ok);
        Assert.Contains("userdata boom", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncUserdataMethodCancellationIsTerminal()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);
        var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
        using var cts = new CancellationTokenSource();

        await using var session = lua.Session(sandbox, services: services);
        var run = session.Run<bool>("""
            local ok = pcall(function()
                return async_user.new('Suri'):delayed_name()
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
    public async Task AsyncUserdataMethodKeepsReceiverAliveDuringSuspension()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_user.new('Suri'):delayed_name()").AsTask();

        await service.WaitForStartedCountAsync(1);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        service.Complete("alive");

        var result = await run;

        Assert.True(result.Ok);
        Assert.Equal("Suri:alive", result.Unwrap());
        Assert.True(service.CreatedPlayer.TryGetTarget(out _));
    }

    [Fact]
    public async Task AsyncUserdataFailureDoesNotPoisonLaterRunOnSameSession()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var failedRun = session.Run("return async_user.new('Suri'):delayed_name()").AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Fail(new InvalidOperationException("first failed"));

        var failed = await failedRun;
        var succeededRun = session.Run<string>("return async_user.new('Suri'):delayed_name()").AsTask();

        await service.WaitForStartedCountAsync(2);
        service.Complete("ok");

        var succeeded = await succeededRun;

        Assert.False(failed.Ok);
        Assert.True(succeeded.Ok);
        Assert.Equal("Suri:ok", succeeded.Unwrap());
    }

    [Fact]
    public async Task SameSessionRunDuringAsyncUserdataSuspensionThrows()
    {
        var service = new AsyncUserdataService();
        var lua = Engine.Create(b => b.Module<AsyncUserdataModule>());
        var services = Services.Create().Add(service);

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return async_user.new('Suri'):delayed_name()").AsTask();

        await service.WaitForStartedCountAsync(1);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await session.Run("return 1"));

        service.Complete("done");
        Assert.True((await run).Ok);
    }
}

[Module("async_user")]
public partial class AsyncUserdataModule(AsyncUserdataService service)
{
    [Fn("new")]
    public AsyncPlayer New(string name) => new(service, name);

    [Fn("save_count")]
    public long SaveCount() => service.SaveCount;
}

[Userdata("async_player")]
public partial class AsyncPlayer
{
    private readonly AsyncUserdataService _service;
    private readonly string _name;

    public AsyncPlayer(AsyncUserdataService service, string name)
    {
        _service = service;
        _name = name;
        service.Capture(this);
    }

    [Fn("delayed_name", Async = true)]
    public ValueTask<string> DelayedName([Context] ScriptContext context)
        => _service.Delayed(_name, context.Cancellation);

    [Fn("immediate_name", Async = true)]
    public ValueTask<string> ImmediateName()
        => ValueTask.FromResult("immediate:" + _name);

    [Fn("save", Async = true)]
    public ValueTask Save([Context] ScriptContext context)
        => _service.Save(context.Cancellation);
}

public sealed class AsyncUserdataService
{
    private readonly Queue<TaskCompletionSource<string>> _pending = [];
    private readonly object _gate = new();
    private int _startedCount;

    public WeakReference<AsyncPlayer> CreatedPlayer { get; private set; } = new(null!);
    public int SaveCount { get; private set; }

    public async ValueTask<string> Delayed(string name, CancellationToken cancellation)
    {
        var value = await AwaitNext(cancellation);
        return name + ":" + value;
    }

    public async ValueTask Save(CancellationToken cancellation)
    {
        await AwaitNext(cancellation);
        SaveCount++;
    }

    public void Capture(AsyncPlayer player)
        => CreatedPlayer = new WeakReference<AsyncPlayer>(player);

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

        throw new TimeoutException("Timed out waiting for async userdata call to start.");
    }

    public void Complete(string value)
    {
        if (!TryComplete(value))
            throw new InvalidOperationException("No pending async userdata operation.");
    }

    public void Fail(Exception error)
    {
        while (TryDequeue(out var completion))
        {
            if (completion.TrySetException(error))
                return;
        }

        throw new InvalidOperationException("No pending async userdata operation.");
    }

    private async ValueTask<string> AwaitNext(CancellationToken cancellation)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _pending.Enqueue(completion);
            _startedCount++;
        }

        return await completion.Task.WaitAsync(cancellation);
    }

    private bool TryComplete(string value)
    {
        while (TryDequeue(out var completion))
        {
            if (completion.TrySetResult(value))
                return true;
        }

        return false;
    }

    private bool TryDequeue(out TaskCompletionSource<string> completion)
    {
        lock (_gate)
            return _pending.TryDequeue(out completion!);
    }
}

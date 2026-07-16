namespace PopLua.Tests;

public sealed class CallbackTests
{
    [Fact]
    public async Task FunctionRefCanBeCapturedAndCalledWhileSessionIsAlive()
    {
        var sink = new CallbackSink();
        var services = Services.Create().Add(sink);
        var lua = Engine.Create(b => b.Module<CallbackModule>());

        await using var session = lua.Session(Sandbox.Untrusted, services: services);
        var registered = await session.Run("button.on_click(function(value) return 'clicked:' .. value end)");

        Assert.True(registered.Ok);
        Assert.NotNull(sink.Clicked);
        var callback = sink.Clicked;

        var result = await callback.Call<string>(Value.From("ok"));

        Assert.True(result.Ok);
        Assert.Equal("clicked:ok", result.Unwrap());
    }

    [Fact]
    public async Task FunctionRefCanCallAsyncHostWork()
    {
        var sink = new CallbackSink();
        var services = Services.Create().Add(sink);
        var lua = Engine.Create(b => b.Module<CallbackModule>());

        await using var session = lua.Session(Sandbox.Untrusted, services: services);
        var registered = await session.Run("button.on_click(function(value) return button.delay(value) end)");

        Assert.True(registered.Ok);
        Assert.NotNull(sink.Clicked);
        var callback = sink.Clicked;

        var result = await callback.Call<string>(Value.From("ok"));

        Assert.True(result.Ok);
        Assert.Equal("async:ok", result.Unwrap());
    }

    [Fact]
    public async Task FunctionRefDisposedFailsClearly()
    {
        var sink = new CallbackSink();
        var services = Services.Create().Add(sink);
        var lua = Engine.Create(b => b.Module<CallbackModule>());

        await using var session = lua.Session(Sandbox.Untrusted, services: services);
        (await session.Run("button.on_click(function() return 'ok' end)")).ThrowIfError();

        Assert.NotNull(sink.Clicked);
        var callback = sink.Clicked;
        await callback.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => { _ = callback.Call(); });
    }

    [Fact]
    public async Task FunctionRefAfterSessionDisposeFailsClearly()
    {
        var sink = new CallbackSink();
        var services = Services.Create().Add(sink);
        var lua = Engine.Create(b => b.Module<CallbackModule>());
        var session = lua.Session(Sandbox.Untrusted, services: services);

        (await session.Run("button.on_click(function() return 'ok' end)")).ThrowIfError();
        Assert.NotNull(sink.Clicked);
        var callback = sink.Clicked;

        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await callback.Call());
    }

    [Fact]
    public async Task FunctionRefCannotReenterActiveSession()
    {
        var sink = new CallbackSink();
        var services = Services.Create().Add(sink);
        var lua = Engine.Create(b => b.Module<CallbackModule>());

        await using var session = lua.Session(Sandbox.Untrusted, services: services);
        var result = await session.Run("return button.call_now(function() return 'bad' end)");

        Assert.False(result.Ok);
        Assert.Contains("active execution", result.Error!.Message, StringComparison.Ordinal);
    }
}

[Module("button")]
public partial class CallbackModule(CallbackSink sink)
{
    [Fn("on_click")]
    public void OnClick(FunctionRef callback) => sink.Clicked = callback;

    [Fn("call_now")]
    public string CallNow(FunctionRef callback)
        => callback.Call<string>().AsTask().GetAwaiter().GetResult().Unwrap();

    [Fn("delay", Async = true)]
    public async ValueTask<string> Delay([Context] ScriptContext ctx, string value)
    {
        await Task.Yield();
        ctx.Cancellation.ThrowIfCancellationRequested();
        return "async:" + value;
    }
}

public sealed class CallbackSink
{
    public FunctionRef? Clicked { get; set; }
}

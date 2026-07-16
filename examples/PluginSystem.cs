using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var host = new PluginHost(new ConsolePluginLog());

await host.Load(new Plugin(
    Id: "hello",
    Name: "Hello Plugin",
    Trusted: false,
    OnStart: """
        log.info("started")
        state.set("runs", state.get("runs") + 1)
        """));

await host.StartAll();

public sealed class PluginHost(IPluginLog log)
{
    private readonly List<LoadedPlugin> _plugins = [];

    private readonly Engine _lua = Engine.Create(b => b
        .Module<LogModule>()
        .Module<StateModule>());

    private static readonly Sandbox Basic = Sandbox.Build(b => b
        .Allow(PluginCaps.Log)
        .Allow(PluginCaps.State)
        .Quota(
            instructions: 200_000,
            activeTime: TimeSpan.FromSeconds(2),
            wallTime: TimeSpan.FromSeconds(30)));

    public async Task Load(Plugin plugin)
    {
        await using var session = _lua.Session(Basic, Identity.Create(plugin.Id, plugin.Name));
        var chunk = Chunk.Code(plugin.OnStart, name: $"{plugin.Id}:on_start.lua");
        var bytecode = session.Compile(chunk);

        _plugins.Add(new LoadedPlugin(plugin, bytecode));
    }

    public async Task StartAll()
    {
        foreach (var loaded in _plugins)
        {
            var plugin = loaded.Plugin;
            var scope = new PluginScope(plugin.Id);

            var services = Services.Create()
                .Add(plugin)
                .Add(scope)
                .Add<IPluginLog>(log)
                .Add(new PluginState());

            await using var session = _lua.Session(Basic, Identity.Create(plugin.Id, plugin.Name), services);

            var result = await session.Run(loaded.OnStart);
            if (!result.Ok)
                log.Write(plugin.Id, "error", result.Error!.Message);
        }
    }
}

[Module("log")]
public partial class LogModule(IPluginLog log, PluginScope scope)
{
    [Fn("info")]
    public void Info([Context] ScriptContext ctx, string message)
    {
        ctx.Sandbox.Require(PluginCaps.Log);
        log.Write(scope.PluginId, "info", message);
    }
}

[Module("state")]
public partial class StateModule(PluginState state)
{
    [Fn("get")]
    public long Get([Context] ScriptContext ctx, string key)
    {
        ctx.Sandbox.Require(PluginCaps.State);
        return state.Get(key);
    }

    [Fn("set")]
    public void Set([Context] ScriptContext ctx, string key, long value)
    {
        ctx.Sandbox.Require(PluginCaps.State);
        state.Set(key, value);
    }
}

public sealed record Plugin(string Id, string Name, bool Trusted, string OnStart);
public sealed record LoadedPlugin(Plugin Plugin, Bytecode OnStart);
public sealed record PluginScope(string PluginId);

public sealed class PluginState
{
    private readonly Dictionary<string, long> _values = [];

    public long Get(string key) => _values.TryGetValue(key, out var value) ? value : 0;
    public void Set(string key, long value) => _values[key] = value;
}

public interface IPluginLog
{
    void Write(string pluginId, string level, string message);
}

public sealed class ConsolePluginLog : IPluginLog
{
    public void Write(string pluginId, string level, string message)
        => Console.WriteLine($"[{pluginId}] {level}: {message}");
}

public static class PluginCaps
{
    public const string Log = "plugin.log";
    public const string State = "plugin.state";
}

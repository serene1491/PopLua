using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var services = Services.Create().Add(new PlayerDirectory());

var lua = Engine.Create(b => b.Module<PlayerModule>());

var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
await using var session = lua.Session(sandbox, services: services);

var result = await session.Run<string>("""
    local player = players.find("lancode")
    return player:get_name_async()
    """);

Console.WriteLine(result.Unwrap());

[Module("players")]
public partial class PlayerModule(PlayerDirectory directory)
{
    [Fn("find")]
    public Player Find(string id) => new(directory, id);
}

[Userdata("player")]
public partial class Player(PlayerDirectory directory, string id)
{
    [Fn("get_name_async", Async = true)]
    public ValueTask<string> GetNameAsync([Context] ScriptContext ctx)
        => directory.GetName(id, ctx.Cancellation);
}

public sealed class PlayerDirectory
{
    public async ValueTask<string> GetName(string id, CancellationToken ct)
    {
        await Task.Delay(25, ct);
        return id == "lancode" ? "Pop Lua" : "Unknown Player";
    }
}

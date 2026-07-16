using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var services = Services.Create().Add<IStore>(new MemoryStore());

var lua = Engine.Create(b => b
    .Module<MathModule>()
    .Module<StoreModule>());

var sandbox = Sandbox.Build(b => b
    .Allow(StoreCaps.Read)
    .Allow(StoreCaps.Write));

await using var session = lua.Session(
    sandbox,
    identity: Identity.Create("user-1", "Alice"),
    services: services);

var hits = await session.Run<double>("""
    store.inc("hits", 1)
    store.inc("hits", 4)

    local x = mathx.clamp(store.get("hits"), 0, 10)
    return x
    """);

Console.WriteLine($"hits = {hits.Unwrap()}");

[Module("mathx")]
public partial class MathModule
{
    [Const("PI")]
    public const double Pi = Math.PI;

    [Fn("clamp")]
    public static double Clamp(double value, double min, double max)
        => Math.Clamp(value, min, max);

    [Fn("sum")]
    public static double Sum(Value[] values)
    {
        double total = 0;

        foreach (var value in values)
            if (value.TryNumber(out var n)) total += n;
            else if (value.TryInt(out var i)) total += i;

        return total;
    }

    [Fn("log")]
    public static void Log([Context] ScriptContext ctx, string message)
        => Console.WriteLine($"[{ctx.Identity.Name ?? ctx.Identity.Id}] {message}");
}

[Module("store")]
public partial class StoreModule(IStore store)
{
    [Fn("inc")]
    public int Inc([Context] ScriptContext ctx, string key, int by)
    {
        ctx.Sandbox.Require(StoreCaps.Write);
        return store.Inc(key, by);
    }

    [Fn("get")]
    public int Get([Context] ScriptContext ctx, string key)
    {
        ctx.Sandbox.Require(StoreCaps.Read);
        return store.Get(key);
    }
}

public static class StoreCaps
{
    public const string Read = "store.read";
    public const string Write = "store.write";
}

public interface IStore
{
    int Inc(string key, int by);
    int Get(string key);
}

public sealed class MemoryStore : IStore
{
    private readonly Dictionary<string, int> _data = [];

    public int Inc(string key, int by)
        => _data[key] = Get(key) + by;

    public int Get(string key)
        => _data.TryGetValue(key, out var value) ? value : 0;
}

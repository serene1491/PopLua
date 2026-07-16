using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Module<StorageModule>());

var reader = Sandbox.Build(b => b
    .Allow(Caps.FileRead)
    .Quota(
        instructions: 100_000,
        activeTime: TimeSpan.FromSeconds(1),
        wallTime: TimeSpan.FromSeconds(10)));

var writer = Sandbox.Build(b => b
    .Allow(Caps.FileRead)
    .Allow(Caps.FileWrite)
    .Quota(
        instructions: 100_000,
        activeTime: TimeSpan.FromSeconds(1),
        wallTime: TimeSpan.FromSeconds(10)));

await Try("reader", reader);
await Try("writer", writer);

await using var limited = lua.Session(reader);
var loop = await limited.Run("while true do end");
Console.WriteLine(loop.Ok ? "loop finished" : loop.Error!.Message);

async Task Try(string name, Sandbox sandbox)
{
    await using var session = lua.Session(sandbox);

    var result = await session.Run<string>("""
        if storage.can_write() then
            storage.write("message", "hello")
        end

        return storage.read("message")
        """);

    Console.WriteLine($"{name}: {(result.Ok ? result.Unwrap() : result.Error!.Message)}");
}

[Module("storage")]
public partial class StorageModule
{
    private static readonly Dictionary<string, string> Data = [];

    [Fn("read")]
    public static string Read([Context] ScriptContext ctx, string key)
    {
        ctx.Sandbox.Require(Caps.FileRead);
        return Data.TryGetValue(key, out var value) ? value : "";
    }

    [Fn("write")]
    public static void Write([Context] ScriptContext ctx, string key, string value)
    {
        ctx.Sandbox.Require(Caps.FileWrite);
        Data[key] = value;
    }

    [Fn("can_write")]
    public static bool CanWrite([Context] ScriptContext ctx)
        => ctx.Sandbox.Has(Caps.FileWrite);
}

using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Diagnostics(new ConsoleDiagnostics()));

await using var session = lua.Session(
    Sandbox.Build(b => b
        .AllowSafeLibs()
        .Quota(
            instructions: 10_000,
            activeTime: TimeSpan.FromSeconds(1),
            wallTime: TimeSpan.FromSeconds(10))),
    Identity.Create("script-1", "Diagnostics demo"));

await session.Run(Chunk.Code("return 1 + 1", name: "ok.lua"));
await session.Run(Chunk.Code("""
    local function fail()
        error('bad input')
    end

    fail()
    """, name: "failing.lua"));
await session.Run(Chunk.Code("while true do end", name: "quota.lua"));

public sealed class ConsoleDiagnostics : IDiagnostics
{
    public void Started(ScriptContext ctx, Chunk chunk)
        => Console.WriteLine($"start: {ctx.Identity.Id}:{chunk.Name ?? "anonymous"}");

    public void Completed(ScriptContext ctx, in Metrics metrics)
        => Console.WriteLine($"done: {metrics.Duration.TotalMilliseconds:F1}ms, instructions={metrics.Instructions}, peak={metrics.PeakMemoryBytes}B");

    public void Failed(ScriptContext ctx, RuntimeException error)
    {
        Console.WriteLine($"fail: {error.Message}");

        if (error is ScriptException script && script.LuaTrace is not null)
            Console.WriteLine(script.LuaTrace);
    }

    public void QuotaBlocked(ScriptContext ctx, QuotaKind kind)
        => Console.WriteLine($"quota: {kind}");

    public void SandboxBlocked(ScriptContext ctx, string cap)
        => Console.WriteLine($"sandbox: {cap}");
}

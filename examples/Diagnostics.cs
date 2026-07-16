using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Observe(new ConsoleDiagnostics()));

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

public sealed class ConsoleDiagnostics : IRunObserver
{
    public void Started(ScriptContext ctx, Step step)
        => Console.WriteLine($"start: {ctx.Run.Id}:{step.Kind}:{step.Name}");

    public void Completed(ScriptContext ctx, Step step, in Metrics metrics)
        => Console.WriteLine($"done: {step.Id}: active={metrics.Active.TotalMilliseconds:F1}ms, suspended={metrics.Suspended.TotalMilliseconds:F1}ms, instructions={metrics.Instructions}, peak={metrics.PeakMemoryBytes}B");

    public void Failed(ScriptContext ctx, Step step, RuntimeException error)
    {
        Console.WriteLine($"fail: {error.Message}");

        if (error is ScriptException script && script.LuaTrace is not null)
            Console.WriteLine(script.LuaTrace);
    }

    public void Quota(ScriptContext ctx, Step step, QuotaKind kind)
        => Console.WriteLine($"quota: {kind}");

    public void Denied(ScriptContext ctx, Step step, string cap)
        => Console.WriteLine($"sandbox: {cap}");
}

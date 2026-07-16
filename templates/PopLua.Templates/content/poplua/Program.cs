using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create();
var sandbox = Sandbox.Build(b => b
    .AllowSafeLibs()
    .Quota(
        instructions: 100_000,
        activeTime: TimeSpan.FromSeconds(1),
        wallTime: TimeSpan.FromSeconds(10),
        callDepth: 64));

await using var session = lua.Session(sandbox, Identity.Create("sample"));
var result = await session.Run<long>(Chunk.Code("""
    local values = { 20, 22 }
    return math.max(values[1], values[2]) * 2 - 2
    """, name: "sample.lua"));

if (result.Ok)
    Console.WriteLine($"result: {result.Unwrap()}");
else
    Console.Error.WriteLine(result.Error);

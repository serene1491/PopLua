using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create();

// Untrusted starts closed. Opt into selected native Lua helpers explicitly.
var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
await using var session = lua.Session(sandbox);

var sum = (await session.Run<long>("return 2 + 2")).Unwrap();
Console.WriteLine($"2 + 2 = {sum}");

var error = await session.Run("error('boom')");
if (!error.Ok)
    Console.WriteLine($"Lua error: {error.Error!.Message}");

var named = Chunk.Code("""
    local function add(a, b)
        return a + b
    end

    return add(10, 32)
    """, name: "basic.lua");

Console.WriteLine((await session.Run<long>(named)).Unwrap());

var bytecode = session.Compile("return 1 + 2 + 3", name: "sum.lua");
Console.WriteLine((await session.Run<long>(bytecode)).Unwrap());

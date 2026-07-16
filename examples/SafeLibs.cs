using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create();

// Sandbox.Untrusted has no standard Lua libraries. This profile opts into
// selected native helpers such as math, string, table, utf8, pcall, and type.
var sandbox = Sandbox.Build(b => b.AllowSafeLibs());

await using var session = lua.Session(sandbox);

var result = await session.Run<string>(Chunk.Code("""
    local values = { "pop", "lua" }
    return string.upper(table.concat(values)) .. ":" .. math.max(20, 22)
    """, name: "safe-libs.lua"));

Console.WriteLine(result.Unwrap());

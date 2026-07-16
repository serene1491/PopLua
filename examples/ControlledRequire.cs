using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Require((_, name) =>
    name == "util"
        ? Chunk.Code("return { answer = 42 }", name: "module:util.lua")
        : null));

var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
await using var session = lua.Session(sandbox);

var result = await session.Run<long>(Chunk.Code("""
    local util = require("util")
    return util.answer
    """, name: "controlled-require.lua"));

Console.WriteLine(result.Unwrap());

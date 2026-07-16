using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create();

await using var session = lua.Session(Sandbox.Untrusted);

var result = await session.Run<long>("return 21 * 2");
Console.WriteLine(result.Unwrap());

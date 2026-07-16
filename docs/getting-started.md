# Getting Started

This guide shows the smallest useful PopLua flow: create a runtime, run Lua,
and expose C# functions.

If you are writing Lua scripts for an existing host application, start with the
[Script Author Guide](script-author-guide.md).

## 0. Install

```bash
dotnet add package PopLua --version 1.0.0-rc.1
```

PopLua calls Lua 5.4 or 5.5 through an internal native bridge. Your host environment
must provide a compatible native library available under a supported platform
library name. PopLua prefers 5.5 when both are installed; set
`POPLUA_LUA_VERSION=5.4` or `5.5` to require a version.

Validated Linux setups:

```bash
# Arch Linux: Lua 5.5
sudo pacman -S lua

# Ubuntu 24.04: Lua 5.4
sudo apt-get install liblua5.4-dev
```

The resolver recognizes `lua5.5`, `lua55`, `liblua5.5.so`,
`liblua5.5.dylib`, `lua5.5.dll`, and `lua55.dll`, with equivalent `5.4`
names. If no compatible library is found, PopLua reports the requested version
and the `POPLUA_LUA_VERSION` override.

For a starter project, install the separate template package:

```bash
dotnet new install PopLua.Templates::1.0.0-rc.1
dotnet new poplua
```

For runnable repository examples, see the [examples index](../examples/README.md).

## 1. Create A Runtime

```csharp
using PopLua.Binding;
using PopLua.Context;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create();
```

`Engine` is immutable and can be shared by the host application.

## 2. Run Lua

```csharp
await using var session = lua.Session(Sandbox.Untrusted);

var result = await session.Run<long>("return 21 * 2");
Console.WriteLine(result.Unwrap());
```

`Sandbox.Untrusted` starts without standard Lua libraries or capabilities, which
is the safer default for user-authored scripts. Use `Sandbox.Trusted` only for
trusted host-owned scripts.

Script failures are returned in `Result`; they are not thrown as normal flow.

```csharp
var result = await session.Run("error('boom')");

if (!result.Ok)
    Console.WriteLine(result.Error!.Message);
```

For user-authored scripts, prefer named chunks so errors and diagnostics identify
the script:

```csharp
var result = await session.Run(Chunk.Code(scriptText, name: "plugin:on_start.lua"));

if (result.Error is ScriptException scriptError)
    Console.WriteLine(scriptError.LuaTrace ?? scriptError.Message);
```

## 3. Compile Once, Execute Many

For user-authored scripts that are submitted before they are executed, compile a
named chunk once and store the returned `Bytecode`.

```csharp
Bytecode saved;

await using (var compileSession = lua.Session(Sandbox.Untrusted))
{
    var chunk = Chunk.Code(scriptText, name: "plugin-42:on_start.lua");
    saved = compileSession.Compile(chunk);
}
```

When a trigger occurs, create a fresh session with the trigger identity,
sandbox, and services, then run the cached bytecode:

```csharp
await using var session = lua.Session(
    Sandbox.Untrusted,
    Identity.Create("plugin-42", "Welcome Plugin"),
    services);

var result = await session.Run(saved);
```

Use chunk names that help users locate failures. The bytecode can be reused
across sessions; services and identity still belong to each execution session.
Stable script-visible APIs should be declared as generated modules and userdata,
not by mutating Lua globals dynamically at run time.

## 4. Add A Module

Projects that declare generated PopLua bindings need unsafe blocks enabled
because the generator emits Lua C callback function pointers.

```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

Projects that only create `Engine`/`Session` and do not declare generated
bindings do not need this setting.

For production hosts, a common structure is to keep generated bindings in a
separate class library, such as `MyApp.Scripting`, and enable unsafe blocks only
there. The main console, worker, or server project can remain runtime-only.
See `examples/SplitHost/` for a complete split-project layout.

```csharp
var lua = Engine.Create(b => b.Module<MathModule>());

[Module("mathx")]
public partial class MathModule
{
    [Const("PI")]
    public const double Pi = Math.PI;

    [Fn("add")]
    public static long Add(long a, long b) => a + b;

    [Fn]
    public static string GetUserName(string name) => "user:" + name;
}
```

Lua sees:

```lua
mathx.PI
mathx.add(20, 22)
mathx.get_user_name("Serene")
```

Module functions are ordinary functions stored on a module table, so examples
call them with `.`. Userdata instance methods should be called with `:`, which
passes the userdata receiver for PopLua to map back to the C# instance.

## 5. Use Sandbox Capabilities

```csharp
[Module("secret", Cap = Caps.FileRead)]
public partial class SecretModule
{
    [Fn("value")]
    public static long Value() => 42;
}
```

If the session sandbox does not allow `Caps.FileRead`, the `secret` module is not
registered.

```csharp
var sandbox = Sandbox.Build(b => b.Allow(Caps.FileRead));
await using var session = lua.Session(sandbox);
```

## 6. Opt Into Safe Native Standard Libraries

`Sandbox.Untrusted` starts without Lua standard libraries. Custom sandboxes can
expose a conservative native profile:

```csharp
var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
```

That profile includes selected base helpers plus native `math`, `string`,
`table`, and `utf8`. It does not open `io`, `os`, `package`, `debug`, or Lua
filesystem loading.

## 7. Load Approved Lua Modules

Hosts can opt into controlled `require` for approved reusable Lua chunks:

```csharp
var lua = Engine.Create(b => b.Require((ctx, name) =>
    name == "util"
        ? Chunk.Code("return { message = function() return 'ok' end }", "module:util.lua")
        : null));
```

Lua can then use:

```lua
local util = require("util")
```

The resolver is synchronous and host-owned. PopLua validates strict
dot-separated module names and does not use `package.path`, `package.cpath`, or
filesystem loading by default.

## 8. Use Context

```csharp
[Module("ctx")]
public partial class ContextModule(IReplySink replies)
{
    [Fn("reply", Async = true)]
    public ValueTask Reply([Context] ScriptContext ctx, string message)
        => replies.ReplyAsync(ctx.Identity.Id, message);
}
```

Lua sees a normal generated module:

```lua
ctx.reply("hello")
```

`ScriptContext` is the C# execution context for generated bindings. It is not
itself a Lua global. Use `[Module]` for root API names, `[Context]` for
per-call execution context, and `Services` for per-session host state.

```csharp
[Fn("whoami")]
public static string WhoAmI([Context] ScriptContext ctx)
    => ctx.Identity.Name ?? ctx.Identity.Id;
```

```csharp
await using var session = lua.Session(
    Sandbox.Untrusted,
    Identity.Create("user-1", "Lan"));
```

## 9. Use Services In Instance Modules

Constructor parameters are resolved from the session `IServiceProvider`.

```csharp
var services = Services.Create().Add<IStore>(new MemoryStore());
var lua = Engine.Create(b => b.Module<StoreModule>());

await using var session = lua.Session(Sandbox.Untrusted, services: services);

[Module("store")]
public partial class StoreModule(IStore store)
{
    [Fn("inc")]
    public long Inc(string key, long by) => store.Inc(key, by);
}
```

Lua can call:

```lua
return store.inc("hits", 1)
```

## 10. Accept Structured Descriptor Tables

Lua table descriptors are useful for structured host APIs:

```lua
ui.select("choice", {
  placeholder = "Choose an option",
  tags = { "compact", "searchable" },
  options = {
    { label = "Option A", value = "a" },
    { label = "Option B", value = "b" }
  }
})
```

Expose those as C# descriptor types rooted in generated function signatures:

```csharp
public sealed class SelectDescriptor
{
    public string? Placeholder { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<SelectOptionDescriptor> Options { get; init; } = [];
}

public sealed class SelectOptionDescriptor
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}
```

PopLua generates the table reader and includes descriptor shapes in manifest,
LuaLS, and Markdown API docs. C# `required` descriptor properties must be
present in the Lua table; optional properties can keep normal CLR defaults.
PopLua rejects unknown fields and requires lists to be dense one-based arrays,
so misspelled options and mixed map/array tables fail at the generated boundary.

## 11. Return Structured Tables

Mark output-only DTOs with `[Table]`. Generated bindings copy their public
readable properties into a fresh Lua table:

```csharp
[Table]
public sealed record SaveResult(
    bool Ok,
    [property: Field("scriptId")] string ScriptId,
    IReadOnlyList<string> Warnings);

[Fn("save")]
public static SaveResult Save(string source)
    => new(true, "welcome", []);
```

Nested `[Table]` values and lists are generated recursively. `[Ignore]` excludes
a property, and `[Field]` preserves an explicit Lua-facing name. PopLua bounds
nesting at 64 levels and never exposes a raw or durable Lua table handle.

## 12. Add Host-Controlled Output

`Sandbox.Untrusted` does not open standard Lua libraries, so `print` is not
available by default. Prefer an explicit generated module for script output:

```lua
log.info("loaded")
log.warn("missing optional setting")
log.error("failed")
```

The module can resolve a host logging service through `Services`, receive
`ScriptContext`, and attach identity or sandbox information before storing the
message. `examples/SplitHost/` shows this pattern without making logging a
PopLua framework feature.

## 13. Use Userdata

```csharp
[Module("vec")]
public partial class VecModule
{
    [Fn("new")]
    public static Vec2 New(double x, double y) => new(x, y);
}

[Userdata("vec2")]
public partial class Vec2(double x, double y)
{
    [Prop("x", ReadOnly = true)]
    public double X { get; } = x;

    [Fn("length")]
    public double Length() => Math.Sqrt(X * X + Y * Y);
}
```

Lua sees:

```lua
local v = vec.new(3, 4)
return v:length() + v.x
```

## 14. Use Async Generated Functions

```csharp
[Fn("fetch", Async = true)]
public async ValueTask<string> Fetch([Context] ScriptContext ctx, string id)
{
    await Task.Delay(10, ctx.Cancellation);
    return id;
}
```

Lua calls async generated functions normally:

```lua
local value = api.fetch("42")
return value
```

Userdata instance methods can use the same async bridge:

```csharp
[Userdata("player")]
public partial class Player
{
    [Fn("name", Async = true)]
    public ValueTask<string> Name([Context] ScriptContext ctx)
    {
        return LoadName(ctx.Cancellation);
    }
}
```

## 15. Generate Lua API Tooling Files

Projects with generated bindings can emit three build artifacts:

- `poplua.api.json`: stable machine-readable API manifest.
- `poplua.d.lua`: LuaLS/LuaCATS definitions for editor autocomplete.
- `poplua-api.md`: Markdown API documentation for Lua authors.

Files are generated by default and written to `$(PopLuaApiOutputDir)`, defaulting under
`obj/<configuration>/<target-framework>/poplua-api`. For CI, set an explicit
artifact folder:

```xml
<PopLuaApiOutputDir>$(MSBuildProjectDirectory)/artifacts/poplua-api</PopLuaApiOutputDir>
```

Set `PopLuaGenerateApiManifest`, `PopLuaGenerateLuaLsDefinitions`, or
`PopLuaGenerateApiDocs` to `false` to opt out of a specific output.

For VS Code LuaLS, add the generated definition file to your workspace library:

```json
{
  "Lua.workspace.library": [
    "${workspaceFolder}/artifacts/poplua-api/poplua.d.lua"
  ]
}
```

Manual generation is also available from generated providers:

```csharp
File.WriteAllText("poplua.api.json", PopLua.Generated.PopLuaApiManifestProvider.Json);
File.WriteAllText("poplua.d.lua", PopLua.Generated.PopLuaLuaLsDefinitionProvider.Lua);
File.WriteAllText("poplua-api.md", PopLua.Generated.PopLuaApiDocumentationProvider.Markdown);
```

Provider classes are generated by default unless the corresponding generation
property is set to `false`. Providers are internal to that assembly, so manual
export code must run from the project that defines the generated bindings.
Summaries, remarks, parameter docs, return docs, examples, and exception docs
from XML comments flow into the manifest and Markdown output when available.

# Technical Reference

## Architecture

```text
Host app
  -> Engine
  -> Session
  -> generated module bindings
  -> internal LuaStack / LuaNative
  -> Lua 5.4 or 5.5 C API
```

The public API is intentionally small. Internal types such as `LuaStack`,
`LuaStateHandle`, and `LuaNative` are implementation details.

## Package Requirements

```bash
dotnet add package PopLua --version 1.0.0-rc.1
```

PopLua requires a compatible Lua 5.4 or 5.5 native library available under a supported platform library name.
Projects that declare generated bindings must enable
`<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.

The resolver recognizes these names:

| Version | Linux/generic | macOS | Windows |
|---|---|---|---|
| 5.5 | `lua5.5`, `lua55`, `liblua5.5.so` | `liblua5.5.dylib` | `lua5.5.dll`, `lua55.dll` |
| 5.4 | `lua5.4`, `lua54`, `liblua5.4.so` | `liblua5.4.dylib` | `lua5.4.dll`, `lua54.dll` |

Linux x64 is the validated RC platform. Ubuntu CI runs Lua 5.4 and Arch Linux
CI runs Lua 5.5. Recognizing a Windows or macOS name is not a validation claim
for those platforms.

The unsafe requirement is scoped to projects that declare bindings. A host can
put `[Module]` and `[Userdata]` types in a separate binding library and
keep the main runtime-only app project free of unsafe blocks.

Native Lua binaries are not bundled in this preview. Packaging native assets in
the future would be a deployment-convenience feature, not a performance
optimization for the current architecture.

## AOT Status

`1.0.0-rc.1` has been validated with `dotnet publish -c Release -r
linux-x64 /p:PublishAot=true` for runtime-only use, generated module bindings,
generated userdata bindings, and async generated bindings. Published
applications still need the Lua 5.4 or 5.5 native library available under a
supported platform library name at run time. The RC does not claim portable
NativeAOT support beyond Linux x64.

## Runtime

`Engine` stores:

- generated module descriptors;
- default services;
- diagnostics;
- allocator options.

`Session` owns one `lua_State`. It is not thread-safe. Create separate
sessions for concurrent script execution.

## Scripting Platform Workflow

PopLua already supports the common host workflow without a separate scripting
model:

```text
script submitted
-> named Chunk created
-> compiled to Bytecode
-> bytecode stored by the host
-> trigger occurs
-> fresh Session created with identity, sandbox, and services
-> bytecode executed
-> Result and diagnostics captured
```

Recommended pattern:

- Share one `Engine` for the host application.
- Compile submitted scripts through `Session.Compile(...)` using a stable
  `Chunk` name.
- Store `Bytecode` in the host's own cache or database.
- Create a new `Session` for each isolated execution or trigger.
- Pass trigger-specific state through `Identity`, `Services`, and
  generated modules.
- Treat `Result` failures as script feedback and host misuse exceptions as
  application bugs.

Reuse a session only when the script environment should intentionally keep Lua
globals between calls. Sessions are single-execution at a time and reject
same-session reentry while active or suspended.

`Bytecode` is cache-friendly: it owns its byte array and can be reused across
sessions created by the same host process. It is not a replacement for host-side
authorization, versioning, or source storage; keep the original source when users
need editable scripts or precise review history.

Do not accept bytecode from untrusted users. Prefer compiling reviewed source
through `Session.Compile(...)` so the host controls the Lua 5.4 or 5.5 runtime,
source identity, and audit trail. Bytecode compatibility is tied to Lua's
bytecode format and PopLua's Lua 5.4 or 5.5 runtime expectations.

`Chunk.Code(...)` encodes a C# string to a new UTF-8 byte array.
`Chunk.Utf8(...)` keeps the supplied `ReadOnlyMemory<byte>`; do not mutate or
release that memory before the chunk has been compiled or run.
`Chunk.File(...)` is host-side file access, not Lua sandboxed IO. It reads the
file bytes and uses the path as the chunk name.

## Production Checklist

- Use `Sandbox.Untrusted`, or a stricter custom sandbox, for user-authored scripts.
- Do not use `Sandbox.Trusted` for user-authored scripts.
- Opt into only the native Lua standard libraries a script environment needs.
- Name every submitted script with `Chunk.Code(script, name: ...)`.
- Compile submitted source to `Bytecode`; do not accept arbitrary external
  Lua bytecode.
- Store bytecode only from trusted compilation and keep source for review/editing.
- Create a fresh `Session` per isolated trigger unless shared Lua state is intentional.
- Pass identity, services, sandbox, and cancellation per execution.
- Declare public Lua API roots as generated `[Module]` types. Do not build
  stable script APIs through dynamic global mutation.
- Collect `IDiagnostics` events and final `Metrics`.
- Treat `Caps.*` as labels; host APIs must enforce real IO/network/process policy.
- Provide host-controlled logging/output through generated modules.
- Provide a compatible Lua 5.4 or 5.5 native library available under a supported platform library name.
- Dispose sessions.

## Native Standard Libraries

`Sandbox.Untrusted` exposes no native Lua standard libraries by default.
Custom sandboxes can opt into selected native Lua 5.4 or 5.5 libraries:

```csharp
var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
```

The safe profile includes:

- selected base functions: `assert`, `error`, `ipairs`, `pairs`, `pcall`,
  `select`, `tonumber`, `tostring`, and `type`;
- native `math`;
- native `string`;
- native `table`;
- native `utf8`.

It does not expose `collectgarbage`, `dofile`, `load`, `loadfile`, `print`,
raw/metatable helpers, `_G`, `_VERSION`, `io`, `os`, `package`, or `debug`.

For explicit profiles, use:

```csharp
var sandbox = Sandbox.Build(b => b.AllowLibs(
    Library.Math | Library.String | Library.Table | Library.Utf8));
```

`Library.Package`, `Io`, `Os`, `Debug`, and `FullBase` are available
only as explicit host choices. Avoid them for untrusted user-authored scripts
unless the host deliberately accepts the corresponding native Lua behavior.

`Sandbox.Trusted` opens all standard libraries and allows all capabilities. It
is for trusted host-owned scripts only.

Native library selection is independent from PopLua capabilities and generated
modules. It also remains separate from PopLua's controlled `require`, which
does not use `package.path`, `package.cpath`, or filesystem search.

## Controlled Loading

PopLua provides opt-in host-managed `require` through
`EngineBuilder.Require`:

```csharp
var lua = Engine.Create(b => b.Require((ctx, name) =>
    name == "util"
        ? Chunk.Code("return { message = function() return 'ok' end }", "module:util.lua")
        : null));
```

The resolver receives the current `ScriptContext` and a normalized module name. It
returns an approved `Chunk` or `null` when the module is unavailable. Session
services can override the runtime resolver by providing a `ModuleResolver`
service.

Controlled loading is deliberately smaller than Lua's standard package system:

- no resolver means PopLua does not install `require`;
- no filesystem loading is built in;
- `package.path` and `package.cpath` are not used;
- module names must be dot-separated identifiers such as `util` or
  `game.player`;
- names with path traversal, slashes, drive prefixes, control characters,
  `.lua` suffixes, empty segments, or more than 128 characters are rejected;
- resolver callbacks are synchronous;
- loaded module return values are cached per session;
- a module returning `nil` is cached as `true`, matching Lua's normal
  `require` convention;
- failed loads are not cached;
- cycles fail fast instead of exposing partially initialized modules.

Failure categories are intentionally small and stable: missing modules report
`module not found: <name>`, invalid names report `invalid module name: <name>`,
cycles report the detected chain, and resolver exceptions report
`module resolver failed: <name>: <host message>`.

The loaded chunk runs as a normal Lua function on the active PopLua coroutine,
so generated host functions and async module calls inside module initialization
use the existing scheduler, quotas, diagnostics, cancellation, and traceback
behavior.

When Lua's `error` and `pcall` are available, missing, invalid, cyclic, and
runtime module-load failures are Lua errors and can be caught by `pcall`.
`Sandbox.Untrusted` does not open those standard functions by default; in that
case PopLua uses its managed error side-channel and the execution fails through
`Result.Error`. Host cancellation remains terminal.

Capability policy is host-owned. Installing a resolver is the opt-in switch for
`require`; hosts that want capability-gated loading can check
`ctx.Sandbox.Require("script.load")` inside the resolver or provide the resolver
only for sessions that should load modules.

Async resolvers, automatic filesystem search, package managers, mod manifests,
and a public `package.loaded` table are not included in this preview.

## Results

Script errors are returned:

```csharp
Result result = await session.Run("error('bad')");

if (!result.Ok)
    Console.WriteLine(result.Error);
```

Host misuse can still throw directly, such as using a disposed session.

When `Result.Ok` is `false`, `Result.Error` contains the failure and
`Result.Value` is `Value.Nil`. When `Result<T>.Ok` is `false`,
`Result<T>.Error` contains the failure and `Value` is default. `Unwrap()` and
`ThrowIfError()` throw the stored `RuntimeException`. `Or(...)` intentionally hides
the failure behind a fallback; avoid it when quota, sandbox, or script failures
must be reported to users.

`ScriptException` preserves script-facing failure details when Lua provides
them:

- `Message`: Lua's error message, usually including `chunk:line`.
- `Chunk`: the chunk name parsed from the Lua error or supplied by
  `Chunk.Name`.
- `Line`: the Lua source line parsed from the error when available.
- `LuaTrace`: Lua's real traceback for runtime errors when available.

Use named chunks for user-authored scripts. Names are passed to Lua with the
standard `@name` form so errors read like `plugin:on_start.lua:17: ...`.
Compile errors include chunk and line information but normally do not have a
runtime traceback because the chunk did not execute.

Synchronous generated callback failures still use PopLua's managed-error side
channel and are not reliably catchable by Lua `pcall`: PopLua returns a failed
`Result`, but the protected Lua call may report success. This is intentional
for `1.0` because PopLua avoids native `lua_error`/longjmp across managed
callback frames. If Lua scripts need catchable host failures, expose the
operation as an async generated function returning `ValueTask`/`ValueTask<T>` or
return an explicit status value. Async task faults are raised by the Lua
coroutine wrapper and are catchable by `pcall` unless host cancellation
terminates the execution.

## Diagnostics And Analytics

Configure diagnostics once on the runtime:

```csharp
var lua = Engine.Create(b => b.Diagnostics(new MyDiagnostics()));
```

`IDiagnostics` receives:

- `Started(ctx, chunk)` for chunk execution and `Call(...)` execution.
- `Completed(ctx, metrics)` after successful execution.
- `Failed(ctx, error)` after script, quota, sandbox, cancellation, or async
  execution failure.
- `QuotaBlocked(ctx, kind)` before `Failed` when a quota stops execution.
- `SandboxBlocked(ctx, cap)` when a denied capability blocks use.

`ScriptContext` carries identity, sandbox, services, cancellation, and live
execution state. `Metrics` carries final wall-clock duration, approximate
instruction count, peak Lua allocator usage, and maximum Lua call depth. Hosts
remain responsible for storing and aggregating diagnostics.

`Metrics.Duration` is total wall-clock execution duration, including time
spent suspended on async operations. `activeTime` quota enforcement counts active
Lua execution and host binding work. For generated async functions, suspended
await time is excluded from `activeTime` by default and can opt back in with
`[Fn(Async = true, PauseTime = false)]`. `wallTime` always counts total
elapsed execution lifetime, including waits.

`ExecutionState` is live state visible during callbacks. `Metrics` is the
final completed-execution snapshot. `PeakMemoryBytes` counts Lua allocator
memory only, not managed allocations performed by host callbacks or services.

## Script Output

Untrusted sessions do not open standard libraries, so `print` is unavailable by
default. Trusted sessions use Lua's normal `print` behavior. For user-authored
scripts, prefer an explicit generated module such as `log.info(...)` so output
can include identity, sandbox checks, and host-owned storage.

A typical shape is:

```csharp
public interface IScriptLog
{
    void Write(ScriptContext context, ScriptLogLevel level, string message);
}

[Module("log", Cap = "script.log")]
public partial class LogModule(IScriptLog log)
{
    [Fn("info")]
    public void Info([Context] ScriptContext ctx, string message)
        => log.Write(ctx, ScriptLogLevel.Info, message);
}
```

Register the module normally and provide the log service through
`Services`. The capability is just a host-defined label; the service decides
where output goes and how long it is retained. `examples/SplitHost/` shows this
pattern in a runtime-only app plus generated-binding library.

## Sandbox

`Sandbox.Untrusted` starts with no capabilities, no standard Lua libraries, and
default instruction, active-time, and call-depth quotas. Use it, or a stricter
custom policy, for user-authored scripts. `Sandbox.Trusted` opens standard Lua
libraries and allows all capabilities; reserve it for trusted host-owned scripts.
Modules with a capability are not registered unless the sandbox allows it.

```csharp
var sandbox = Sandbox.Build(b => b
    .Allow(Caps.FileRead)
    .Quota(
        instructions: 100_000,
        activeTime: TimeSpan.FromSeconds(1),
        wallTime: TimeSpan.FromSeconds(30)));
```

`Sandbox.Build(...)` starts from an empty policy, not from `Sandbox.Untrusted`.
Configure quotas explicitly for custom untrusted policies. Capabilities are
host-defined strings. `Caps.FileRead`, `Caps.Net`, and the other built-in names
do not enforce OS permissions by themselves; enforcement happens when a module
declares `[Module(Cap = "...")]` or host code calls
`ctx.Sandbox.Require(...)`.

`AllowAll()` affects capability checks only. `Deny(cap)` blocks a capability,
including when `AllowAll()` is set; a later `Allow(cap)` removes that deny.

Resource-control status:

| Control | Status |
| --- | --- |
| Instruction quota | Enforced by Lua debug hooks on execution coroutine threads. |
| Active-time quota | Counts active Lua execution and host binding work. Suspended async waits pause it by default unless the generated function sets `PauseTime = false`. Arbitrary synchronous C# can only be stopped at PopLua checkpoints. |
| Wall-time quota | Counts total elapsed execution lifetime, including Lua execution, synchronous host work, and suspended async waits. It is the absolute lifetime limit for an execution. |
| Cancellation | Terminal for active executions; a caller-provided token is observed by the debug hook while Lua is running and by async waits while suspended. |
| Memory quota | Enforced by PopLua's internal Lua allocator. Failures surface as `QuotaException` with `QuotaKind.Memory`. |
| GC threshold | Enforced by PopLua's internal Lua allocator and debug hook; the hook triggers Lua collection after tracked heap usage crosses the configured threshold. |
| Call-depth quota | Enforced by Lua debug call/return hooks and counts nested Lua function calls on the execution coroutine. |

Async generated bindings can choose how suspended await time affects
`activeTime`:

```csharp
[Fn("wait", Async = true, PauseTime = true)]
public static async ValueTask Wait(double seconds, [Context] ScriptContext ctx)
    => await Task.Delay(TimeSpan.FromSeconds(seconds), ctx.Cancellation);

[Fn("build_index", Async = true, PauseTime = false)]
public static async ValueTask BuildIndex([Context] ScriptContext ctx)
    => await SomeHostWorkAsync(ctx.Cancellation);
```

`PauseTime` defaults to `true` for async generated functions. It only affects
the interval when the operation is actually suspended. C# work before the first
await and after resumption still counts as active time. `wallTime` always
continues to run.

## Source Generator

Projects that declare generated bindings need:

```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

This is required for projects defining `[Module]`, `[Fn]`, or
`[Userdata]` APIs because generated callbacks use unmanaged function
pointers. Projects that only consume `Engine` without declaring generated
bindings do not need unsafe blocks.

Implemented generator support:

- `[Module]` on `partial` classes/structs.
- static `[Fn]` methods.
- instance `[Fn]` methods with public constructor services resolved from
  `ScriptContext.Services`.
- static `[Const]`.
- stored and computed `[Prop]`.
- `[Context]` as first parameter.
- `FunctionRef` parameters for session-owned callbacks.
- `Value[]` as the final variadic parameter.
- `Value[]` return for multiple Lua returns.
- `[Userdata]` metatables with methods, readable properties, optional
  setters, `__tostring`, `__gc`, and supported operators.
- async module functions and userdata instance methods with
  `[Fn(Async = true)]`, `ValueTask`, and `ValueTask<T>`.
- source-visible registration with `Module<T>()`, `Modules<T1, T2>()`, or a
  host-written `ModuleCollection` callback.

Lua call-style mapping:

- Generated module functions are stored on module tables and are normally
  called with `.`, such as `log.info("hello")`.
- Generated userdata instance methods are stored on the userdata metatable and
  are normally called with `:`, such as `player:rename("Suri")`.
- Lua `:` syntax passes the userdata as the first Lua argument. PopLua consumes
  that receiver internally to recover the C# instance; the C# method uses
  `this`, not a declared `Value self` parameter.
- The manual Lua receiver form, `player.rename(player, "Suri")`, follows normal
  Lua rules and is equivalent for userdata methods. Calling
  `player.rename("Suri")` omits the receiver and fails as a type error.

Generator caveats:

- Generated binding projects require `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- If a binding project forgets that setting, the generator reports `PLUA010`.
- Optional C# parameters are not optional Lua parameters in this preview.
- Generic Lua-exposed methods/classes are not supported as generated bindings.
- Duplicate Lua names in the same module or userdata produce `PLUA008`.
- `ReadOnlySpan<byte>` parameters are valid only during the generated callback
  and must not be stored.
- `Value` table/function/userdata values are opaque and should not be treated
  as durable handles.
- `Value` can represent ordinary Lua arguments where supported, but it is
  not used for the userdata receiver. A userdata method parameter named
  `Value self` is rejected with `PLUA011`.
- `Value[]` as a final parameter collects remaining arguments; returning an
  empty `Value[]` returns no Lua values.

Common generator diagnostics are listed below. See
[Diagnostics](diagnostics.md) for descriptions and fixes.

| Code | Meaning |
|---|---|
| `PLUA001` | `[Fn]` method is not public. |
| `PLUA002` | Unsupported marshaling type. |
| `PLUA003` | `[Module]` type is not partial. |
| `PLUA004` | `[Context]` parameter is not first. |
| `PLUA005` | Async function does not return `ValueTask` / `ValueTask<T>`. |
| `PLUA006` | `[Userdata]` type is not partial. |
| `PLUA007` | `Value[]` parameter is not last. |
| `PLUA008` | Duplicate Lua name in a module or userdata. |
| `PLUA010` | Generated binding project does not enable unsafe blocks. |
| `PLUA011` | Userdata method declares `Value self`; use the C# instance receiver instead. |
| `PLUA012` | `PauseTime` is set on a non-async `[Fn]` method. |

## API Tooling Outputs

The generator produces Lua API tooling artifacts by default for projects with
generated bindings. Internal C# providers are generated during compilation and
MSBuild writes external files from those providers after compilation. The
generator does not write files directly.

| Property | Default |
|---|---|
| `PopLuaGenerateApiManifest` | `true` |
| `PopLuaGenerateLuaLsDefinitions` | `true` |
| `PopLuaGenerateApiDocs` | `true` |
| `PopLuaApiOutputDir` | `obj/<configuration>/<target-framework>/poplua-api` |
| `PopLuaApiManifestFile` | `poplua.api.json` |
| `PopLuaLuaLsDefinitionFile` | `poplua.d.lua` |
| `PopLuaApiDocsFile` | `poplua-api.md` |

Generated providers are available to the same project:

```csharp
PopLua.Generated.PopLuaApiManifestProvider.Json
PopLua.Generated.PopLuaLuaLsDefinitionProvider.Lua
PopLua.Generated.PopLuaApiDocumentationProvider.Markdown
```

Each provider is generated by default unless the matching generation property is
set to `false`.

All outputs are derived from the internal manifest model. They do not inspect
runtime state and do not use runtime reflection.

Generated providers are internal to the consumer assembly. Manual provider
usage must happen in the same assembly that defines the generated bindings.

Manifest documentation currently preserves XML summaries, remarks, parameter
descriptions, return descriptions, examples, and exception descriptions when
Roslyn provides them. Type-parameter docs, obsolete/deprecation metadata, and
default parameter values are not manifest fields in this preview.

## Marshaling

Supported generated module parameter/return types:

- `bool`
- `int`, `uint`, `long`, `ulong`
- `float`, `double`
- `string`
- `Value`
- `FunctionRef` parameters for session-owned Lua callbacks
- descriptor/table DTO parameters rooted in exposed signatures
- final parameter `Value[]`
- return `Value[]`
- `[Userdata]` parameters and returns
- injected `[Context] ScriptContext`

Async module functions and userdata instance methods support the same
Lua-facing return types via `ValueTask<T>`, plus `ValueTask` for no Lua return
values. `Task` and `Task<T>` are intentionally unsupported for generated async
bindings in `1.0`; use `ValueTask`/`ValueTask<T>` instead. Async properties,
fields, operators, constructors, and finalizers are not generated as async Lua
functions.

Module fields and C# properties marked with `[Prop]` are stored module
values registered when a session is created. Module methods marked with
`[Prop]` are computed properties and may receive `[Context]`:

```csharp
[Module("ctx")]
public static partial class ContextModule
{
    [Prop("author")]
    public static LuaUser Author([Context] ScriptContext context)
    {
        var request = (RunRequest)context.Services.GetService(typeof(RunRequest))!;
        return new LuaUser(request.Author);
    }
}
```

Lua reads that property with normal field syntax:

```lua
local name = ctx.author.username
```

Computed module properties are generated module members, not dynamically
injected globals. Use module functions for actions such as `ctx.reply(...)`,
and computed module properties for context-dependent values such as
`ctx.author`.

Null and nil behavior:

- Returning `null` from a generated `string` return pushes Lua nil.
- Passing Lua nil to a `string` parameter is a type error in generated bindings.
- Returning `null` userdata pushes Lua nil.
- Passing Lua nil to a userdata parameter is a type error.

Descriptor table parameters:

- Plain C# descriptor classes with a public parameterless constructor and public
  set/init properties can be used as generated function parameters.
- Supported descriptor member types are primitives, `string`, `Value`,
  `[Userdata]`, nested descriptor classes, and
  `IReadOnlyList<T>`/`IList<T>`/`List<T>` of descriptor classes or strings.
- Nullable scalar descriptor fields accept nil or omission without losing the
  property's CLR initializer.
- C# `required` descriptor properties must be present in the Lua table.
- Descriptor fields use snake_case by default; `[Field("exactName")]`
  selects an explicit Lua-visible field name.
- Missing required fields and wrong nested field types report descriptor paths
  such as `select_descriptor.options[2].label`.
- Unknown descriptor fields are rejected so misspelled options cannot silently
  fall back to CLR defaults.
- Lists must be dense one-based Lua arrays. Named, sparse, and mixed tables are
  rejected with the list field path.
- Descriptor discovery is rooted in Lua-exposed signatures; PopLua does not
  scan and expose arbitrary assembly types.
- Descriptors are copied into C# data during the call and are represented in
  `poplua.api.json`, `poplua.d.lua`, and generated Markdown docs.
- Types marked with `[Table]` are output-only DTOs. Generated bindings copy
  public readable properties, nested `[Table]` values, and bounded lists into
  fresh Lua tables.
- `[Ignore]` excludes output properties and `[Field]` controls field names.
  Generated output rejects object graphs deeper than 64 levels.
- Public Lua table references remain outside the `1.0` surface.

`Value[]` varargs accept nil, booleans, integers, numbers, strings, tables,
functions, and userdata values. Tables, functions, and userdata are opaque
`ValueKind` entries for inspection only; use typed descriptor parameters,
`FunctionRef`, or typed userdata parameters when C# needs to interact with
those values. Returning a `Value[]` can push only nil, booleans, numbers,
integers, and strings.

Userdata notes:

- Classes and structs can be exposed as userdata when marked `partial`.
- Userdata wrappers allocate Lua userdata and a managed handle; allocation-heavy
  loops are currently expensive compared with primitive module calls.
- `Setters = true` generates setters only for writable marshalable members.
- Supported operators map to Lua metamethods for arithmetic/comparison forms
  documented in the spec. Unsupported signatures produce generator diagnostics.
- `__gc` releases the managed handle when enabled.

Session lifetime:

- A `Session` owns its Lua state, globals, loaded module values, closures, and
  userdata wrappers.
- PopLua uses Lua globals internally to install generated modules, controlled
  `require`, and sandbox-selected native libraries. That mutation is
  PopLua-owned runtime setup, not an embedder API for constructing public Lua
  surfaces.
- `FunctionRef` holds a Lua closure alive while its owning session is alive.
  Dispose the reference when the host no longer needs it. Calling a function ref
  uses the same execution, async, cancellation, diagnostics, and reentry rules as
  `Session.Call(...)`.
- Lua function refs are not serializable and are not durable event
  subscriptions. For interactions that may happen after a session is disposed,
  store host-side IDs or routing records, then create a session and run or call a
  named script entry point when the external event arrives.

## Async Bridge

Async module functions and userdata instance methods are exposed through
generated Lua coroutine wrappers and the PopLua scheduler. Generated managed
callbacks start the `ValueTask` and return an internal operation token; they do
not call `lua_yieldk`. The Lua wrapper yields with that token when the operation
is incomplete, and `Session` resumes the coroutine after completion.

Cancellation through `ScriptContext.Cancellation` is terminal for the active
execution and is not caught by Lua `pcall`. Managed async task faults are raised
by the Lua wrapper as Lua errors, so `pcall` can catch them. A `Session`
rejects same-session reentrant `Run`/`Call` while an execution is active or
suspended.

## Services

Generated instance modules are created when Lua calls an instance function. The
generator chooses the public constructor with the most parameters and resolves
each parameter by exact type from `ScriptContext.Services`.

Missing services become Lua errors and are returned through `Result`.

## Native Interop

The internal bridge directly calls Lua 5.4 or 5.5. Important native calls include:

- `lua_newstate`
- `luaL_loadbufferx`
- `lua_resume`
- `lua_tolstring`
- `lua_sethook`
- `lua_gc`
- `lua_dump`

The zero-copy UTF-8 path is `LuaStack.ReadStringUtf8`, backed by `lua_tolstring`.

# PopLua Specification v2

> A modern, fast Lua runtime for C#.
> One package, source-generated bindings, AOT-first, sandboxed by default.

PopLua exists to make Lua embedding boring in the best way: simple host code,
clear script APIs, low allocation cost, and no runtime reflection on the hot path.

## 1. Goals

| Goal | Decision |
|---|---|
| Simple first use | One public package: `PopLua` |
| Fast calls | Generated bindings, function pointers, static marshaling |
| NativeAOT friendly | No `dynamic`, no runtime assembly scanning, no expression compilation |
| Safe by default | Scripts start with no capabilities and small quotas |
| Productive API | One namespace, short names, convention over configuration |
| Clear extension model | Attributes for bindings, strings for capabilities |

Performance matters, but the user-facing API should stay small. Advanced pieces
such as the Lua C bridge, marshaling tables, and generated registration are
internal details unless an application has a real reason to go lower.

## 2. Package And Layout

Users install only:

```xml
<PackageReference Include="PopLua" Version="1.0.0-rc.1" />
```

The package contains:

- `PopLua.dll`: runtime, context, sandbox, diagnostics, marshaling, binding contracts.
- `PopLua.Interop`: internal direct Lua 5.4 or 5.5 C API bridge.
- `PopLua.Generators`: Roslyn analyzer/source generator shipped as an analyzer.
- Attribute definitions in the main public API.

### 2.1 Native Lua Decision

PopLua uses a small internal P/Invoke layer over the Lua 5.4 or 5.5 C API.

Reasons:

- `lua_tolstring` must be available for zero-copy `ReadOnlySpan<byte>` string reads.
- Debug hooks, callbacks, bytecode dumping, and stack operations need a very small,
  predictable surface.
- AOT analysis is simpler when the interop layer is explicit and internal.

Distribution requirement: the host must provide a compatible Lua 5.4 or 5.5 native
library available under a supported platform library name. Packaging native Lua binaries can be added
later without changing the public API.

Recommended repository layout:

```text
PopLua/
├── src/
│   ├── PopLua/
│   │   ├── Runtime/
│   │   ├── Context/
│   │   ├── Binding/
│   │   ├── Sandbox/
│   │   ├── Marshaling/
│   │   ├── Diagnostics/
│   │   └── Interop/
│   └── PopLua.Generators/
├── tests/
└── examples/
```

Internal projects may be split for maintainability, but the external product is
the single `PopLua` package.

## 3. Public API Shape

Most applications should need only:

```csharp
using PopLua.Binding;
using PopLua.Context;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Module<MyModule>());

await using var session = lua.Session(Sandbox.Untrusted);
var result = await session.Run<long>("return 21 * 2");

Console.WriteLine(result.Unwrap());
```

Public names are intentionally short:

| Concept | Public name |
|---|---|
| Runtime host | `Engine` |
| Stateful Lua execution scope | `Session` |
| Explicit execution context | `ScriptContext` |
| Source input | `Chunk` |
| Precompiled chunk | `Bytecode` |
| Generic Lua value | `Value` |
| Result wrapper | `Result`, `Result<T>` |
| Sandbox policy | `Sandbox` |
| Script identity | `Identity` |

All common types live in `PopLua`. Internal implementation can still use folders
and namespaces, but sample code should not require namespace hopping.

## 4. Lua Semantics

| C# declaration | Lua exposure | Lua syntax |
|---|---|---|
| Static method in `[Module]` | module table function | `mathx.clamp(x, a, b)` |
| Instance method in `[Module]` | module table function | `store.get("key")` |
| Method in `[Userdata]` | userdata method | `v:length()` |
| Property in `[Userdata]` | userdata field | `v.x` |

Modules never receive an implicit `self`. Userdata methods always receive the
userdata at Lua stack index 1 because Lua desugars `obj:method(x)` into
`obj.method(obj, x)`. Generated userdata callbacks consume that receiver
internally; C# userdata methods use `this` and must not declare a `Value self`
parameter.

## 5. Attributes

```csharp
namespace PopLua;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ModuleAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public string? Cap { get; init; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class FnAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
    public bool Async { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method)]
public sealed class PropAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
    public bool ReadOnly { get; init; }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ConstAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IgnoreAttribute : Attribute;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ContextAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class UserdataAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public bool Setters { get; init; }
    public bool ToString { get; init; } = true;
    public bool Gc { get; init; } = true;
}
```

Rules:

- `[Module]` and `[Userdata]` types must be `partial`.
- `[Fn]` methods must be public.
- Projects that declare generated bindings must enable
  `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`. Runtime-only projects do not
  need unsafe blocks.
- If a function name is omitted, `PascalCase` becomes `snake_case`.
- `[Context] ScriptContext ctx` is injected and must be the first parameter.
- `Value[]` collects remaining Lua arguments and must be the last parameter.
- `[Module(Cap = "...")]` skips registration when the active sandbox lacks the capability.

## 6. Runtime API

```csharp
public sealed class Engine : IAsyncDisposable
{
    public static Engine Create(Action<EngineBuilder>? configure = null);

    public Session Session(
        Sandbox? sandbox = null,
        Identity? identity = null,
        IServiceProvider? services = null);

    public ValueTask<Result> Run(
        string code,
        Sandbox? sandbox = null,
        CancellationToken ct = default);

    public ValueTask<Result<T>> Run<T>(
        string code,
        Sandbox? sandbox = null,
        CancellationToken ct = default);
}

public sealed class EngineBuilder
{
    public EngineBuilder Module<T>();
    public EngineBuilder Modules(Action<ModuleCollection> add);
    public EngineBuilder Services(IServiceProvider services);
    public EngineBuilder Diagnostics(IDiagnostics diagnostics);
    public EngineBuilder Allocator(AllocatorOptions options);
    public EngineBuilder Require(ModuleResolver resolver);
}

public delegate Chunk? ModuleResolver(ScriptContext context, string moduleName);
```

`Engine` is immutable after creation and can be shared. `Session` owns one
`lua_State` and is not thread-safe.

`Require(...)` installs PopLua's controlled Lua `require` function. The
resolver receives a normalized dot-separated module name and returns a
host-approved `Chunk`, or `null` when the module is unavailable. PopLua does
not use `package.path`, `package.cpath`, or filesystem loading by default.

```csharp
public sealed class Session : IAsyncDisposable
{
    public Identity Identity { get; }
    public Sandbox Sandbox { get; }

    public ValueTask<Result> Run(string code, CancellationToken ct = default);
    public ValueTask<Result<T>> Run<T>(string code, CancellationToken ct = default);
    public ValueTask<Result> Run(Chunk chunk, CancellationToken ct = default);
    public ValueTask<Result<T>> Run<T>(Chunk chunk, CancellationToken ct = default);

    public ValueTask<Result> Call(string global, params Value[] args);
    public ValueTask<Result<T>> Call<T>(string global, params Value[] args);

    public Bytecode Compile(string code, string? name = null);
    public Bytecode Compile(Chunk chunk);
    public ValueTask<Result> Run(Bytecode bytecode, CancellationToken ct = default);
    public ValueTask<Result<T>> Run<T>(Bytecode bytecode, CancellationToken ct = default);
}
```

Async methods use short names intentionally. They return `ValueTask` and are
awaited by normal C# call sites.

Current implementation status:

- `Engine`, `Session`, source chunks, bytecode, global calls, controlled
  host-managed `require`, diagnostics, module registration, and resource quota
  enforcement are implemented.
- Trusted sessions open standard Lua libraries. Untrusted sessions start without
  standard libraries by default.
- Custom sandboxes can opt into selected native Lua standard libraries with
  `AllowLibs(...)` or the conservative
  `AllowSafeLibs()` profile.
- Script errors are returned as `Result` failures.

```csharp
public readonly record struct Chunk
{
    public string? Name { get; }

    public static Chunk Code(string code, string? name = null);
    public static Chunk File(string path);
    public static Chunk Utf8(ReadOnlyMemory<byte> code, string? name = null);
}

public sealed class Bytecode
{
    public ReadOnlyMemory<byte> Data { get; }
    public string? Name { get; }
}

public readonly record struct AllocatorOptions(
    nuint InitialHeapBytes = 0,
    nuint MaxHeapBytes = 0,
    nuint GcThresholdBytes = 0);
```

## 7. Context, Identity, Services

```csharp
public sealed class ScriptContext
{
    public Identity Identity { get; }
    public Sandbox Sandbox { get; }
    public IServiceProvider Services { get; }
    public CancellationToken Cancellation { get; }
    public ExecutionState State { get; }
}

public sealed class Identity
{
    public string Id { get; }
    public string? Name { get; }
    public IReadOnlyDictionary<string, object> Tags { get; }

    public static Identity Anonymous { get; }
    public static Identity System { get; }
    public static Identity Create(string id, string? name = null,
        IReadOnlyDictionary<string, object>? tags = null);
}

public sealed class Services : IServiceProvider
{
    public static Services Create();
    public Services Add<T>(T instance) where T : notnull;
    public object? GetService(Type serviceType);
}

public sealed class ExecutionState
{
    public long Instructions { get; }
    public long PeakMemoryBytes { get; }
    public int CallDepth { get; }
    public TimeSpan Elapsed { get; }
}
```

`Services` is a tiny built-in service container for examples and small hosts.
Applications may pass any `IServiceProvider`.

## 8. Sandbox

```csharp
public sealed class Sandbox
{
    public static Sandbox Untrusted { get; }
    public static Sandbox Trusted { get; }
    public static Sandbox Build(Action<Builder> configure);

    public Library Libs { get; }
    public bool Has(string cap);
    public void Require(string cap);
}

public sealed class Builder
{
    public Builder Allow(string cap);
    public Builder Deny(string cap);
    public Builder AllowAll();
    public Builder Quota(
        long? instructions = null,
        TimeSpan? activeTime = null,
        TimeSpan? wallTime = null,
        int? callDepth = null,
        int hookInterval = 1000);
    public Builder Memory(nuint? heapBytes = null, nuint? gcBytes = null);
    public Builder AllowLibs(Library libraries);
    public Builder AllowSafeLibs();
}

[Flags]
public enum Library
{
    None = 0,
    SafeBase = 1,
    Coroutine = 2,
    Math = 4,
    String = 8,
    Table = 16,
    Utf8 = 32,
    FullBase = 64,
    Package = 128,
    Io = 256,
    Os = 512,
    Debug = 1024,
    Safe = SafeBase | Math | String | Table | Utf8,
    All = FullBase | Coroutine | Math | String | Table | Utf8 | Package | Io | Os | Debug,
}

public static class Caps
{
    public const string FileRead = "fs.read";
    public const string FileWrite = "fs.write";
    public const string Net = "net.outbound";
    public const string Process = "proc.spawn";
    public const string Env = "env.read";
    public const string Debug = "debug";
    public const string Profiling = "profiling";
}
```

Sandbox checks happen in two places:

- Module registration: `[Module(Cap = Caps.FileRead)]` hides the whole module
  when the capability is absent.
- Binding code: `ctx.Sandbox.Require(Caps.FileWrite)` blocks a specific action.

Quota enforcement uses the Lua debug hook for instruction counting and elapsed
active Lua execution time checks. A caller-provided cancellation token is also
observed by the hook while Lua is running. Call-depth quota uses debug
call/return hooks on the active coroutine. Memory quota uses PopLua's internal
Lua allocator, and GC threshold requests are observed by the allocator and
serviced by the debug hook.

## 9. Values And Results

```csharp
public readonly struct Value
{
    public ValueKind Kind { get; }
    public bool IsNil { get; }

    public static Value Nil { get; }
    public static Value From(bool value);
    public static Value From(long value);
    public static Value From(double value);
    public static Value From(string value);

    public bool TryBool(out bool value);
    public bool TryInt(out long value);
    public bool TryNumber(out double value);
    public bool TryString(out string? value);

    public bool Bool();
    public long Int();
    public double Number();
    public string String();
}

public enum ValueKind : byte
{
    Nil,
    Bool,
    Int,
    Number,
    String,
    Table,
    Function,
    Userdata,
}

public readonly struct Result
{
    public bool Ok { get; }
    public Value Value { get; }
    public RuntimeException? Error { get; }
    public void ThrowIfError();
}

public readonly struct Result<T>
{
    public bool Ok { get; }
    public T? Value { get; }
    public RuntimeException? Error { get; }
    public T Unwrap();
    public T Or(T fallback);
}
```

Script errors are normal results, not thrown exceptions. System failures may
still throw.

## 10. Marshaling

Supported parameters:

- `bool`, `int`, `uint`, `long`, `ulong`, `float`, `double`
- `string`
- `ReadOnlySpan<byte>` for zero-copy UTF-8 strings valid during the call
- `Value`
- `FunctionRef` for session-owned Lua function callbacks
- `Value[]` as the final variadic parameter
- any `[Userdata]` type
- descriptor/table DTO classes rooted in exposed function signatures
- dense string lists in exposed parameters and descriptor fields
- `[Context] ScriptContext` as the first injected parameter

Supported returns:

- `void`
- `bool`, `int`, `uint`, `long`, `ulong`, `float`, `double`
- `string`
- `Value`
- `Value[]` for multiple Lua return values
- any `[Userdata]` type
- `ValueTask` and `ValueTask<T>` when `[Fn(Async = true)]` is set

Unsupported types produce generator error `PLUA002`.
Userdata values inside `Value[]` varargs remain unsupported; expose typed
userdata parameters instead.

Stable Lua-facing APIs should be declared as generated modules, userdata, and
descriptor parameters. PopLua may mutate Lua globals internally for runtime
bootstrapping, module installation, controlled `require`, and sandbox setup, but
public embedder APIs should not be constructed through dynamic global mutation.

## 11. Userdata

`[Userdata]` exposes a managed type as Lua userdata.

Generated metatables include:

- `__index` for methods and readable properties.
- `__newindex` only when `Setters = true`.
- `__tostring` when enabled.
- `__gc` when enabled.
- Operator metamethods for supported C# operators:
  `__add`, `__sub`, `__mul`, `__div`, `__unm`, `__eq`, `__lt`, `__le`.

Example:

```csharp
[Userdata("vec2")]
public partial class Vec2(double x, double y)
{
    [Prop("x", ReadOnly = true)] public double X { get; } = x;
    [Prop("y", ReadOnly = true)] public double Y { get; } = y;

    [Fn("length")]
    public double Length() => Math.Sqrt(X * X + Y * Y);

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
}
```

## 12. Async Bridge

Async Lua functions are generated from:

```csharp
[Fn("fetch", Async = true)]
public async ValueTask<string> Fetch([Context] ScriptContext ctx, string id)
{
    return await service.Fetch(id, ctx.Cancellation);
}
```

PopLua runs async-capable executions on Lua coroutine threads owned by
`Session`. Generated async callbacks start the `ValueTask` and return an
internal operation token. They must not directly call `lua_yieldk` or otherwise
longjmp out of managed callback frames. A PopLua-generated Lua wrapper yields
the coroutine with that token, and `Session` resumes the coroutine when the
operation completes. From Lua, the call looks synchronous:

```lua
local body = http.fetch("/status")
```

Async support covers generated module functions and userdata instance methods.
Both use the same coroutine bridge: generated callbacks start the
`ValueTask`/`ValueTask<T>` and return an internal operation token. For userdata
methods, the receiver is read before suspension and the managed async state
machine keeps it alive until completion.

Cancellation uses `ScriptContext.Cancellation`, is terminal for the active
execution, and completes the script with a `ScriptException`. Managed async
task faults are raised by the Lua wrapper as Lua errors, so `pcall` can catch
them; host cancellation is not catchable by Lua `pcall`. A session rejects
same-session reentrant `Run` or `Call` while an execution is active or
suspended. `activeTime` counts active Lua and host binding work; `wallTime`
counts total elapsed execution lifetime, including waits. Async generated
functions default to pausing active-time while actually suspended, and can opt
out with `[Fn(Async = true, PauseTime = false)]`.

## 13. Diagnostics

```csharp
public interface IDiagnostics
{
    void Started(ScriptContext ctx, Chunk chunk);
    void Completed(ScriptContext ctx, in Metrics metrics);
    void Failed(ScriptContext ctx, RuntimeException error);
    void QuotaBlocked(ScriptContext ctx, QuotaKind kind);
    void SandboxBlocked(ScriptContext ctx, string cap);
}

public readonly record struct Metrics(
    TimeSpan Duration,
    long Instructions,
    long PeakMemoryBytes,
    int MaxCallDepth);

public enum QuotaKind
{
    Instructions,
    Time,
    Memory,
    CallDepth,
}
```

The default diagnostics implementation is a no-op singleton, so the runtime does
not branch on null.

## 14. Exceptions

```csharp
public abstract class RuntimeException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class ScriptException(string message, string? trace = null)
    : RuntimeException(message)
{
    public string? LuaTrace { get; }
    public string? Chunk { get; init; }
    public int? Line { get; init; }
}

public sealed class SandboxException(string cap)
    : RuntimeException($"Capability '{cap}' is not allowed.");

public sealed class QuotaException(QuotaKind kind)
    : RuntimeException($"Lua quota exceeded: {kind}.");

public sealed class TypeException(string expected, ValueKind actual)
    : RuntimeException($"Expected {expected}, got {actual}.");
```

Execution methods capture these exceptions in `Result`. Host bugs, invalid
configuration, or disposed objects may still throw directly.

Lua runtime failures should preserve Lua-provided script location data where it
exists. `ScriptException.Message` carries Lua's error message, `Chunk` and
`Line` carry parsed location data when available, and `LuaTrace` carries Lua's
real traceback for runtime failures when available. Hosts should name submitted
chunks so diagnostics can identify user-authored scripts.

## 15. Generated Code

The source generator emits:

- one generated binding callback per `[Fn]`;
- module registration for every `[Module]`;
- static `[Const]`, stored `[Prop]` values, and computed `[Prop]`
  module properties into module tables;
- metatable registration for every `[Userdata]`;
- static marshaling for supported types;
- source-visible registration through `Module<T>()`, `Modules<T1, T2>()`, or a
  host-written `ModuleCollection` callback.

Future manifest outputs must be generated from the same semantic model as
bindings. PopLua should not add a separate metadata scanner or runtime
reflection path for documentation, autocomplete, or tooling.

```csharp
public interface IGeneratedModule
{
    static abstract string Name { get; }
    static abstract string? Cap { get; }
    static abstract void Register(Registration registration);
}

public sealed class ModuleCollection
{
    public void Add<T>();
    public void Remove(string name);
}
```

Generated binding callbacks use:

- function pointers for Lua C callbacks;
- `ref struct LuaStack` for stack operations;
- static abstract marshaling interfaces;
- no delegates, closures, boxing, or reflection on the call path.

Compile-time diagnostics:

| Code | Condition |
|---|---|
| `PLUA001` | `[Fn]` on a non-public method |
| `PLUA002` | unsupported parameter or return type |
| `PLUA003` | `[Module]` type is not `partial` |
| `PLUA004` | `[Context]` parameter is not first |
| `PLUA005` | `[Fn(Async = true)]` without `ValueTask` or `ValueTask<T>` |
| `PLUA006` | `[Userdata]` type is not `partial` |
| `PLUA007` | `Value[]` is not the last parameter |
| `PLUA008` | duplicate Lua name in the same module or userdata |
| `PLUA010` | generated binding project does not enable unsafe blocks |
| `PLUA011` | userdata method declares `Value self` instead of using the C# instance receiver |

Current implementation status:

- Implemented: static `[Module]`, static `[Fn]`, `[Context]`, `Value[]`
  variadic parameters, `FunctionRef` callback parameters, `Value[]`
  multiple returns, static `[Const]`, stored and computed module
  `[Prop]`, instance module methods with constructor services from
  `IServiceProvider`, userdata
  metatables, userdata properties and setters, userdata operators, async module
  functions, async userdata instance methods, assembly registration, and
  diagnostics listed above.

## 15.1 API Manifest

PopLua includes a generator-built API manifest as the canonical description of
the Lua surface exposed by generated modules and userdata.

The manifest is intended to support:

- JSON API inspection and compatibility analysis.
- LuaLS / LuaCATS definitions.
- Markdown documentation generation.
- Sandbox-aware API visibility.
- Future documentation and tooling ecosystems.

The manifest must be built from Roslyn symbols already used by the source
generator. It must not use runtime reflection, runtime assembly scanning, or Lua
interop internals.

The manifest schema version must be independent from the PopLua package version.
The canonical model must include both `manifestVersion` and `popluaVersion` so
tools can detect schema changes separately from runtime releases.

Manifest entries must have stable Lua-facing ids, such as `module:vec.new` or
`userdata:vec2.__add`, for external compatibility analysis, documentation links, and editor tooling.
Ids are case-sensitive and are based on the exposed Lua names, not normalized C#
names. C# symbol names and source locations are source metadata, not stable Lua
API identity, and source locations must remain optional diagnostic data.

The manifest must use a canonical `LuaApiType` model instead of exposing raw
Roslyn types to emitters. Type metadata should represent Lua-facing concepts such
as nil, boolean, integer, number, string, userdata references, `Value`,
variadic arguments, multiple returns, void, and future extensions. Async-ness is
function metadata rather than a primitive type, and multiple returns must be
represented as ordered return metadata rather than emitter-specific strings.

Generated tooling documentation preserves XML summaries, remarks, parameter
descriptions, return descriptions, examples, and exception descriptions when the
compiler provides them. Type-parameter docs, obsolete/deprecation metadata, and
default parameter values are deferred until they become Lua-facing binding or
compatibility semantics.

Tooling outputs are generated by default and build-integrated. The default
output files are `poplua.api.json`, `poplua.d.lua`, and
`poplua-api.md` under `$(PopLuaApiOutputDir)`, which defaults below the
intermediate output directory. The source generator emits provider source; an
MSBuild target writes external files from those providers. The generator must
not write arbitrary external files directly.

Standard Lua library metadata should be PopLua-owned and versioned because it is
runtime policy, not user source code. Profile-specific documentation and definitions
are projections over manifest ids; they must not change `Sandbox` runtime
semantics or require runtime sandbox objects to be serialized.

The manifest must remain a build-time metadata product. It must not become
runtime reflection, runtime API discovery, a second binding system, a public
runtime dependency, or an exposure path for Lua interop internals.

Interpolated-string-handler Lua chunk builders are a possible future ergonomics
feature, but they are not part of the manifest architecture.

## 16. Interop Internals

The Lua bridge is internal and unsafe by design.

```csharp
internal readonly record struct LuaStateHandle(nint Value);

internal ref struct LuaStack
{
    public T Read<T>(int index) where T : ILuaReadable<T>;
    public void Push<T>(T value) where T : ILuaPushable<T>;

    public long ReadInt(int index);
    public double ReadNumber(int index);
    public ReadOnlySpan<byte> ReadStringUtf8(int index);
    public void PushStringUtf8(ReadOnlySpan<byte> value);
}

internal interface ILuaReadable<T>
{
    static abstract T Read(LuaStack stack, int index);
}

internal interface ILuaPushable<T>
{
    static abstract void Push(LuaStack stack, T value);
}
```

PopLua uses direct P/Invoke internally. That choice must not leak into the user
API.

## 17. Design Rules

- Prefer examples that fit on one screen.
- Public API should favor `Engine.Create`, `runtime.Session`, `session.Run`.
- Keep low-level types internal unless a real public scenario requires them.
- Avoid hidden global state except generated module registration metadata.
- Keep files small and focused.
- Use strings for capabilities so applications can define their own permission model.
- Preserve AOT compatibility in every feature.
- Treat sandbox and quota failures as normal script results.

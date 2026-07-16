# PopLua Maturity Notes

PopLua `1.0.0-rc.1` is release-candidate software. Lua-facing APIs are declared
in C# source and emitted
by the source generator into runtime registration, manifests, LuaLS definitions,
and Markdown docs.

## Runtime / Execution

| Area | Status |
|---|---|
| Lua 5.4 or 5.5 hosting | RC |
| `Engine` / `Session` | RC |
| `Run`, `Compile`, and `Call` | RC |
| Named chunks and bytecode | RC |
| `Result` error flow and tracebacks | RC |
| Cancellation and debug-hook interruption | RC |
| Instruction, active-time, wall-time, memory, GC, and call-depth quotas | RC |
| Async coroutine bridge | RC |
| Session-owned values, callbacks, and loaded module cache | RC |

Lua values, closures, userdata wrappers, loaded modules, and callback references
belong to the `Session` that created them. `FunctionRef` is session-owned
and is not a durable callback handle.

## Source-Generated Binding Surface

| Area | Status |
|---|---|
| Modules, functions, constants, stored module values, and computed module properties | RC |
| Userdata methods, properties, setters, `__tostring`, `__gc` | RC |
| Userdata operators/metamethods | RC |
| Async module functions and async userdata methods | RC |
| `[Context]` and `Services` | RC |
| Manifest generation | RC |
| LuaLS generation | RC |
| Generated Markdown API docs | RC |

`ScriptContext` is the C# execution context for generated bindings. It is not a
Lua-visible `ctx` object. For script-visible roots such as `ctx`, declare a
generated `[Module("ctx")]` and resolve per-session state through
`Services` or `[Context]`. Use computed module properties for
context-dependent values such as `ctx.author`, and module functions for actions
such as `ctx.reply(...)`.

## Data Interop

| Area | Status |
|---|---|
| Primitive values, strings, nil/null | RC |
| Nullable strings and null userdata returns | RC |
| Ordinary `Value` parameters and returns | RC |
| `[Userdata]` parameters and returns | RC |
| Nested userdata through exposed signatures | RC |
| Structured descriptor/table parameters | RC |
| `[Table]` copy-based return values and nested lists | RC |
| Descriptor metadata in manifest/LuaLS/Markdown | RC |
| Final `Value[]` varargs and multiple returns | RC |
| Table/function/userdata values inside `Value[]` varargs | Opaque inspection only |

Descriptor conversion is generated and copy-based.
Supported descriptor members are primitives, `string`, `Value`, generated
userdata, nested descriptor classes, string lists, and lists of descriptor
classes. Required descriptor properties fail clearly when missing, unknown
fields are rejected, and nested field errors include descriptor paths. Lists
must be dense one-based Lua arrays. PopLua does not expose raw public Lua table
references. Return DTOs opt in with `[Table]`; PopLua copies their public
readable members into a fresh table and bounds recursive output depth.

## Sandbox / Safety

| Area | Status |
|---|---|
| `Sandbox.Untrusted` closed default | RC |
| `Sandbox.Trusted` for host-owned scripts | RC |
| `Library`, `AllowLibs(...)`, and `AllowSafeLibs()` | RC |
| Capability-gated generated modules | RC |
| Controlled host-managed `require` | RC |

## Tooling / Packaging

| Area | Status |
|---|---|
| NuGet runtime package | RC |
| Separate template package | RC |
| Examples | RC |
| Unit/integration test coverage | RC |
| Benchmark coverage | RC baseline |
| NativeAOT posture | Validated on Linux x64 |
| Native Lua external dependency | Validated on Linux x64 |
| Bundled/RID-specific native Lua packages | Out of scope |
| Manual sandbox/profile and Lua standard-library docs | RC |
| Generated standard Lua catalog/profile metadata | Out of scope |

## Platform Claim

Linux x64 is the validated RC platform. NativeAOT consumers, clean package
consumers, examples, and templates are exercised there. Lua 5.4 is tested on
Ubuntu and Lua 5.5 on Arch Linux. Other platforms are not claimed by this RC.

PopLua intentionally does not plan runtime reflection discovery, public raw Lua
stack manipulation, durable closure serialization, or typed userdata transport
through `Value[]` varargs.

`activeTime` and `wallTime` are explicit preview quota concepts. `activeTime`
counts active Lua and host binding work; `wallTime` is the total elapsed
execution lifetime. Async generated functions pause active-time while suspended
by default and may opt out with `PauseTime = false`. Arbitrary synchronous C#
host work cannot be interrupted until PopLua reaches a safe checkpoint.

Generated async bindings support `ValueTask` and `ValueTask<T>` as the stable
`1.0` async return shapes. `Task` and `Task<T>` are intentionally rejected by
generator diagnostics to keep the bridge shape small and allocation-conscious.

Synchronous generated callback failures are a stable `1.0` caveat: PopLua
captures the failure and returns a failed `Result`, but Lua `pcall` may report
success for that protected call. Async task faults and controlled `require`
failures are raised as Lua errors where the coroutine wrapper can do so safely.

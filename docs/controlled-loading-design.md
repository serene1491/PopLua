# Controlled Loading Design

This is a maintainer design note for host-managed script loading.

`0.1.0-preview.2` includes the first minimal implementation: opt-in
host-managed `require` with strict module names, a synchronous resolver,
per-session Lua value caching, named chunks, and no filesystem search by
default. This note records the boundaries that should remain true as the loader
expands.

## Goal

Host applications often need scripts to share reusable Lua code:

```lua
local utils = require("utils")
```

PopLua supports this without enabling arbitrary filesystem access,
`package.path`, or the trusted Lua `package` library for user-authored scripts.

## Current Guidance

Hosts still own script storage and approval. Compile submitted entrypoint
scripts with stable `Chunk` names, cache resulting `Bytecode`, and execute
bytecode in fresh sessions. Expose reusable host behavior through generated
modules.

Do not enable `Sandbox.Trusted` just to get Lua's standard `require` for
untrusted scripts.

For reusable Lua code, enable `EngineBuilder.Require(...)` or provide a
`ModuleResolver` service. The resolver should return only host-approved
source or bytecode with stable chunk names.

## Design Constraints

The PopLua loader should continue to:

- work with `Sandbox.Untrusted`;
- be opt-in and host-provided;
- avoid filesystem loading by default;
- validate module names before resolving them;
- preserve stable chunk names for diagnostics and tracebacks;
- allow hosts to cache approved source or `Bytecode`;
- cache module return values per Lua session, like Lua `require`;
- fail missing, invalid, or cyclic loads clearly;
- surface load failures as Lua errors where possible so `pcall` can observe
  them;
- reuse existing diagnostics and execution context instead of adding a second
  telemetry system;
- keep runtime reflection and public native interop out of the design;
- support Lua modules that return tables or primitive values;
- not break async module calls made during module loading.

## Implementation Shape To Preserve

The runtime shape is a Lua wrapper around small internal callbacks:

```text
require("utils")
-> Lua wrapper checks the per-session module cache
-> internal callbacks validate names and ask the host for approved source or bytecode
-> resolver callback loads the chunk onto the active coroutine stack
-> Lua wrapper calls the loaded chunk
-> Lua wrapper caches the returned Lua value in the session
```

The cache must live in Lua, not in C# `Value`, because module return values
can be Lua tables and functions. `Value` intentionally does not expose durable
table/function handles.

The loaded chunk should execute as a normal Lua function on the active coroutine
so async module calls can still yield through PopLua's existing scheduler.

## Strategy Decisions

- The Lua-facing name is `require`. A PopLua-specific loader name would avoid
  collision, but it would also teach a non-standard script composition model
  that hosts would later need to undo.
- The first implementation is synchronous and in-memory or host-resolved. Async
  resolver work, filesystem conventions, package managers, and mod manifests
  remain separate features.
- The module cache should be per session, matching Lua's normal `require`
  lifetime. Hosts that want cross-session reuse should cache approved source or
  `Bytecode` outside PopLua and let each session cache returned Lua values.
- Cyclic loads fail fast in the first implementation. Exposing partially
  initialized modules like Lua's package table requires more public behavior and
  should wait until there is a clear compatibility reason.
- Module names are strict by default: dot-separated identifiers only. Empty
  names, path traversal, slashes, absolute paths, drive prefixes, `.lua`
  suffixes, embedded nulls/control characters, and host-specific path syntax are
  rejected before calling the resolver.
- Load failures should become Lua errors when the call is executing on the Lua
  side, so `pcall` can catch missing, invalid, and cyclic modules when `pcall`
  exists in the sandbox. Host cancellation should remain terminal, matching the
  rest of PopLua's cancellation behavior.
- A built-in capability is not required for the foundation. Resolver presence is
  the opt-in switch; hosts can additionally gate the loader or APIs with their
  own capability names. A `Caps.ModuleLoad` constant can be revisited if common
  host practice converges on one label.

## Resolver Shape

The public API is intentionally small:

```csharp
public delegate Chunk? ModuleResolver(ScriptContext context, string moduleName);

var lua = Engine.Create(b => b.Require((ctx, name) =>
    name == "utils" ? Chunk.Code(source, "module:utils.lua") : null));
```

Session-specific services can override the runtime resolver by providing a
`ModuleResolver` service. This lets one shared runtime serve multiple
tenants without making module loading a global application decision.

For the first preview, `Chunk?` is intentionally enough:

- returned chunk: module found;
- `null`: module not found;
- resolver exception: host resolver failure.

A richer result type that distinguishes not found, denied, and failed outcomes
can be added later as an overload if real hosts need that precision. It is not
part of the first preview surface so the loader stays small.

## Error And Yield Strategy

The hard part is not resolving text by name. The hard part is preserving
PopLua's safety and error rules:

- Generated managed callbacks do not call `lua_error` because Lua errors use
  native longjmp and must not cross managed callback frames.
- `Sandbox.Untrusted` does not open Lua's base library, so `error`, `pcall`,
  `type`, `tostring`, and standard `require` are unavailable by default.
- Missing, invalid, and cyclic module loads still need useful errors.
- Runtime errors from loaded modules should preserve the loaded module chunk
  name in tracebacks.
- Module loading should not make async module calls fail with
  "yield across C-call boundary" behavior.

The implementation uses a Lua-side wrapper. Resolver callbacks never call
`lua_error`; they return loader functions or error messages. When Lua's `error`
exists, the wrapper raises module-load failures as Lua errors so `pcall` can
catch them. When it does not exist, the wrapper uses PopLua's managed error
side-channel and the execution fails through `Result.Error`.

Public error text should stay concise and script-facing:

- `module not found: util`
- `invalid module name: ../util`
- `cyclic module load: a -> b -> a`
- `module resolver failed: util: <host message>`

The loaded module chunk is called by Lua on the active coroutine, not by a
managed callback. That preserves existing async-yield behavior for generated
async module functions used during module initialization.

## Deferred Expansions

- Async resolvers.
- Built-in filesystem conventions.
- Mod manifests or package-manager behavior.
- A public `package.loaded` table.
- Partially initialized cyclic modules.
- Dedicated module-load diagnostics beyond normal execution diagnostics.
- A built-in `Caps.ModuleLoad` constant.

## Open Questions

- How much module-load activity should appear in `IDiagnostics` beyond the
  current started/completed/failed execution callbacks.
- How cancellation should interrupt a load that performs host work.
- Whether a built-in capability name is useful, or whether hosts should continue
  defining their own capability labels.

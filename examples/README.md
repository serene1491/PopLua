# PopLua Examples

Start with the runtime-only examples, then move to generated bindings and the
split host when you want a production shape.

## Runtime-Only

These projects do not need `AllowUnsafeBlocks`.

| Example | Teaches | Run |
|---|---|---|
| `Minimal` | Smallest `Engine` + `Session` flow. | `dotnet run --project examples/Minimal.csproj` |
| `Basics` | Results, named chunks, script errors, and bytecode. | `dotnet run --project examples/Basics.csproj` |
| `SafeLibs` | Opting into PopLua's safe native Lua library profile. | `dotnet run --project examples/SafeLibs.csproj` |
| `ControlledRequire` | Host-managed `require` without filesystem search. | `dotnet run --project examples/ControlledRequire.csproj` |
| `Diagnostics` | `IDiagnostics`, tracebacks, quotas, and metrics. | `dotnet run --project examples/Diagnostics.csproj` |

## Generated Bindings

These projects declare `[Module]` or `[Userdata]`, so their project files
enable `AllowUnsafeBlocks`.

| Example | Teaches | Run |
|---|---|---|
| `Modules` | Module functions, constants, capabilities, services, and `ScriptContext`. | `dotnet run --project examples/Modules.csproj` |
| `Userdata` | Userdata construction, properties, `:` instance calls, `tostring`, and operators. | `dotnet run --project examples/Userdata.csproj` |
| `ContextModule` | Context-like root API modeled as a generated `ctx` module with computed properties backed by `Services`. | `dotnet run --project examples/ContextModule.csproj` |
| `Descriptors` | Structured Lua table descriptors converted into C# DTOs. | `dotnet run --project examples/Descriptors.csproj` |
| `Callbacks` | Session-owned Lua function references captured by host modules. | `dotnet run --project examples/Callbacks.csproj` |
| `Async` | Async module functions with `ValueTask<T>`. | `dotnet run --project examples/Async.csproj` |
| `AsyncUserdata` | Async userdata methods with `ValueTask<T>`. | `dotnet run --project examples/AsyncUserdata.csproj` |
| `EventHandler` | Trigger-style script execution with injected event/services. | `dotnet run --project examples/EventHandler.csproj` |
| `Sandbox` | Capability-gated generated modules and quotas. | `dotnet run --project examples/Sandbox.csproj` |

## Hosting

| Example | Teaches | Run |
|---|---|---|
| `SplitHost` | Runtime-only app plus unsafe-enabled binding library, host-controlled `log`, controlled `require`, safe libs, diagnostics, metrics, and fresh sessions. | `dotnet run --project examples/SplitHost/SplitHost.App/SplitHost.App.csproj` |
| `PluginSystem` | Compact compile/cache/execute workflow for submitted scripts. | `dotnet run --project examples/PluginSystem.csproj` |

## Tooling

| Example | Teaches | Command |
|---|---|---|
| `ManualDocs` | Writing generated provider content from the same assembly. | `dotnet run --project examples/ManualDocs.csproj` |

Generated-binding examples also emit `poplua.api.json`, `poplua.d.lua`, and
`poplua-api.md` automatically under their `obj/.../poplua-api/` directory
when they build. `ManualDocs` is only for hosts that want to copy the generated
provider strings somewhere else.

The `poplua`, `poplua-bindings`, and `poplua-host` starter templates in the
separate `PopLua.Templates` package mirror the same progression.

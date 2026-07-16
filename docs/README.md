# PopLua Docs

PopLua is a Lua runtime for C# focused on simple hosting, source-generated
bindings, AOT safety, and sandboxed execution.

## Start Here

Install the preview package with:

```bash
dotnet add package PopLua --version 1.0.0-rc.1
```

PopLua requires a Lua 5.4 or 5.5 native library. It selects 5.5 first when both
are installed; set `POPLUA_LUA_VERSION=5.4` or `5.5` to require one version.

| Page | Purpose |
|---|---|
| [Getting Started](getting-started.md) | Run Lua, expose a C# module, handle results. |
| [Script Author Guide](script-author-guide.md) | Lua-side guidance for host APIs, output, errors, and reusable code. |
| [Technical Reference](technical-reference.md) | Runtime, binding generator, sandbox, interop, diagnostics. |
| [Diagnostics](diagnostics.md) | Generator diagnostic codes and fixes. |
| [Maturity Notes](maturity.md) | Preview status, supported surfaces, and intentionally unsupported areas. |
| [Sandbox Profiles](sandbox-profiles.md) | Built-in sandbox profiles, capabilities, quotas, safe libs, and controlled `require`. |
| [Lua Standard Library Catalog](lua-standard-catalog.md) | Manual catalog of PopLua's native Lua 5.4 or 5.5 library exposure policy. |
| [Compatibility Policy](compatibility.md) | Preview/stable compatibility rules for C# APIs, Lua-facing APIs, manifests, and diagnostics. |
| [API Manifest Design](api-manifest-design.md) | Manifest, LuaLS definitions, docs, and future tooling architecture. |
| [Async Coroutine Bridge Design](async-coroutine-bridge-design.md) | Implemented async bridge design for generated modules and userdata methods. |
| [Controlled Loading Design](controlled-loading-design.md) | Host-managed script/module loading design and limits. |
| [Standard Lua Catalog Design](standard-library-catalog-design.md) | Optional design note for Lua standard-library metadata. |
| [Sandbox Profile Tooling Design](sandbox-profile-tooling-design.md) | Optional design note for filtered docs/LuaLS projections. |
| [Native Lua Decision](native-lua-decision.md) | Maintainer rationale for PopLua's internal Lua 5.4 or 5.5 bridge and native-library packaging tradeoffs. |
| [Release Guide](release.md) | Manual release checklist and package validation expectations. |
| [Roadmap](roadmap.md) | What works now and what comes next. |
| [Changelog](../CHANGELOG.md) | Release notes and current preview limitations. |

For production hosting, start with the checklist in the
[Technical Reference](technical-reference.md#production-checklist).
The [examples index](../examples/README.md) groups runtime-only, generated
binding, hosting, and tooling examples.

## Current Status

Working today:

- Lua 5.4 or 5.5 execution through an internal native bridge.
- `Engine` and `Session`.
- `Result` / `Result<T>` error flow.
- Sandboxes with capabilities and instruction, active-time, and wall-time
  quotas.
- Memory quota, GC threshold, and Lua call-depth quota enforcement.
- Source-generated static modules with `[Module]`, `[Fn]`, `[Const]`,
  stored and computed `[Prop]`, `[Context]`, variadic `Value[]`, and
  constructor service injection for instance modules.
- Source-generated userdata with metatables, methods, properties, setters,
  `__tostring`, `__gc`, and supported operators.
- Source-generated strict descriptor/table parameter conversion, including
  nested descriptors and dense string lists rooted in exposed signatures.
- Copy-based `[Table]` return DTOs, nested return DTOs, and bounded lists.
- Async coroutine bridge for source-generated module functions and userdata
  methods.
- Generated JSON manifest, LuaLS definition, and Markdown documentation outputs
  by default.
- Generated tooling documentation preserves XML summaries, remarks, parameters,
  returns, examples, and exceptions when available.
- Bytecode compile/run.
- Controlled host-managed `require` with strict module names, synchronous
  resolver callbacks, named chunks, and per-session module value caching.
- Opt-in native Lua standard-library selection for custom sandboxes, including
  a conservative safe profile.
- Diagnostics hooks.
- Lua runtime errors with chunk, line, and traceback information when available.
- Split-project host example with runtime-only app and unsafe-enabled generated
  binding library.
- Public Lua APIs are expected to be declared in C# source and generated into
  runtime registration, manifest, LuaLS, and Markdown docs.

Intentionally outside the RC:

- Filesystem/package-path loading, async resolvers, package managers, and mod
  manifests for controlled loading.
- Generated standard Lua catalog metadata and sandbox-profile tooling outputs.
- Native Lua binary packaging.
- Platform claims beyond Linux x64.

## API Tooling Quick Start

Projects with generated bindings write `poplua.api.json`, `poplua.d.lua`, and `poplua-api.md` under
`obj/.../poplua-api` by default. Set `PopLuaApiOutputDir` to publish them as CI
artifacts or point VS Code LuaLS at `poplua.d.lua`. Individual outputs can be
disabled by setting the matching `PopLuaGenerate...` property to `false`.

## Templates

Starter projects live in a separate package:

```bash
dotnet new install PopLua.Templates::1.0.0-rc.1
dotnet new poplua
dotnet new poplua-bindings
dotnet new poplua-host
```

`poplua-modhost` remains deferred until mod-manifest guidance is stable.

## Development Validation

Follow [AGENTS.md](../ai/AGENTS.md) for contributor and agent guardrails.

Use the repository scripts for repeatable checks:

```bash
./scripts/check.sh
./scripts/examples.sh
./scripts/smoke.sh
```

## Tiny Example

```csharp
using PopLua.Binding;
using PopLua.Context;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Module<MathModule>());

await using var session = lua.Session(Sandbox.Untrusted);
var result = await session.Run<long>("return mathx.add(20, 22)");

Console.WriteLine(result.Unwrap());

[Module("mathx")]
public partial class MathModule
{
    [Fn("add")]
    public static long Add(long a, long b) => a + b;
}
```

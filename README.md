# Pop's Lua runtime

PopLua is a Lua runtime for C# focused on source-generated bindings,
AOT-friendly execution, and sandboxed scripts.

## Requirements

PopLua uses an internal Lua 5.4 or 5.5 P/Invoke bridge. The host environment must
provide a compatible native library available under a supported platform
library name. PopLua prefers 5.5 when both are installed; set
`POPLUA_LUA_VERSION=5.4` or `5.5` to require a version.
On Arch Linux, `pacman -S lua` provides Lua 5.5. Ubuntu 24.04 CI uses
`apt-get install liblua5.4-dev`. Native binaries are not bundled.

Linux x64 is the validated RC platform. Windows and macOS library names are
recognized by the resolver but are not part of the RC validation claim.

Projects that declare generated bindings (`[Module]`, `[Fn]`, or
`[Userdata]`) must enable unsafe blocks:

```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

Projects that only create `Engine`/`Session` and do not declare generated
bindings do not need unsafe blocks.

## Install

```bash
dotnet add package PopLua --version 1.0.0-rc.1
```

## Quick Example

```cs
using PopLua.Binding;
using PopLua.Context;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create();

var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
await using var session = lua.Session(sandbox);

var result = await session.Run<long>("return math.max(20, 21) * 2");
Console.WriteLine(result.Unwrap());
```

Generated bindings can live in a separate class library, such as
`MyApp.Scripting`, with unsafe blocks enabled there. A runtime-only console or
service project that only references those bindings and creates sessions does
not need unsafe blocks.

## Documentation

Start here:

- [Docs index](docs/README.md)
- [Getting started](docs/getting-started.md)
- [Script author guide](docs/script-author-guide.md)
- [Technical reference](docs/technical-reference.md)
- [Sandbox profiles](docs/sandbox-profiles.md)
- [Lua standard library catalog](docs/lua-standard-catalog.md)
- [Maturity notes](docs/maturity.md)
- [Compatibility policy](docs/compatibility.md)
- [Native Lua decision](docs/native-lua-decision.md)
- [Roadmap](docs/roadmap.md)
- [Changelog](CHANGELOG.md)

For example projects, start with `examples/README.md`. It points to runtime-only
examples, generated binding examples, tooling examples, and the split production
host layout in `examples/SplitHost/`.

Hosts can opt into controlled Lua composition with `EngineBuilder.Require`.
The resolver maps strict dot-separated module names to host-approved
`Chunk` values; PopLua does not use `package.path` or provide filesystem
loading by default.

For first projects, install the separate template package:

```bash
dotnet new install PopLua.Templates::1.0.0-rc.1
dotnet new poplua
dotnet new poplua-bindings
dotnet new poplua-host
```

## Tooling Outputs

PopLua generates Lua API tooling artifacts for projects with generated
bindings. By default files are written under
`obj/<configuration>/<target-framework>/poplua-api/`:

- `poplua.api.json`
- `poplua.d.lua`
- `poplua-api.md`

Use `PopLuaApiOutputDir` to choose a CI/docs output folder.
The generated provider classes used by manual export code are emitted by
default. Set `PopLuaGenerateApiManifest`, `PopLuaGenerateLuaLsDefinitions`,
or `PopLuaGenerateApiDocs` to `false` to opt out of a specific output.

## Common Commands

```bash
dotnet build PopLua.sln
dotnet test PopLua.sln --no-build
dotnet run --project examples/Minimal.csproj
./scripts/examples.sh
./scripts/smoke.sh
./scripts/check.sh
```

PopLua calls Lua 5.4 or 5.5 through its internal native bridge.

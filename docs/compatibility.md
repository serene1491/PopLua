# Compatibility Policy

PopLua is currently pre-1.0. Preview releases may make breaking changes when a
name, signature, generated artifact, or runtime behavior is still being shaped
for the stable release.

This policy describes the intended compatibility story for `1.0` and later.

## Versioning

PopLua packages use SemVer.

- Preview versions may break public APIs, generated output shape, diagnostics,
  templates, or examples when the changelog calls out the change.
- After `1.0`, patch releases should be bug fixes and documentation fixes.
- After `1.0`, minor releases may add APIs, generated metadata, diagnostics, or
  template features without breaking existing supported usage.
- After `1.0`, breaking public API or Lua-facing schema changes require a major
  version unless the affected behavior was explicitly marked experimental.

## C# Public API

The C# compatibility surface includes:

- runtime types such as `Engine`, `Session`, `Chunk`, `Bytecode`,
  `Result`, `Value`, `FunctionRef`, `Sandbox`, and `Library`;
- public attributes such as `ModuleAttribute`, `FnAttribute`,
  `UserdataAttribute`, `PropAttribute`, and `ContextAttribute`;
- `EngineBuilder` registration methods and controlled `require` APIs;
- public exception, diagnostics, metrics, identity, and service types;
- documented MSBuild properties for generated API artifacts;
- template package behavior that appears in generated project files.

After `1.0`, removing or renaming these APIs, changing parameter meaning, or
weakening documented safety/lifetime behavior is a breaking change.

## Lua-Facing API

PopLua host applications define their Lua-facing APIs through generated C#
declarations. Compatibility for those host APIs is represented by
`poplua.api.json`.

Breaking Lua-facing changes usually include:

- removing a module, userdata type, descriptor type, function, value, property,
  or operator;
- changing stable Lua-facing ids such as `module:ctx.reply`;
- changing parameter order, requiredness, or Lua-facing type;
- changing return count or Lua-facing type;
- changing userdata mutability;
- changing required capabilities;
- changing async behavior in a way scripts can observe.

Usually additive changes include adding a new module/member, adding optional
documentation, or improving examples while keeping stable ids and callable
shapes unchanged.

## Manifest Schema

The manifest schema name and version are separate from the NuGet package
version. Consumers should read `schema` and `manifestVersion` before assuming a
shape.

After `1.0`:

- adding optional fields is schema-compatible;
- changing field meaning or removing fields requires a schema-versioned change;
- tools should ignore unknown fields in compatible schema versions;
- tools should fail fast or opt into a compatibility path for unknown major
  schema versions.

## Diagnostics

Generator diagnostics use stable `PLUAxxx` ids.

Before `1.0`, ids may still change if a diagnostic is split or removed. After
`1.0`, diagnostic ids should remain stable whenever practical. Message wording
may improve, but a diagnostic changing from warning to error, or an error being
removed, should be treated as release-note-worthy.

## Explicit Non-Compatibility Surfaces

PopLua does not promise compatibility for:

- internal native Lua interop types;
- generated C# implementation details;
- generated file formatting beyond documented JSON/LuaLS/Markdown semantics;
- BenchmarkDotNet output;
- undocumented environment variables or temporary validation folders.

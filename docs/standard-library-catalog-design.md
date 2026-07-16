# Standard Lua Catalog Design

This is a maintainer design note. It describes future tooling metadata for Lua
5.4 standard libraries. Runtime sandbox policy can already opt into selected
native Lua libraries; the catalog should document and project that real policy
into editor/docs outputs.

## Decision

PopLua now has a manual [Lua Standard Library Catalog](lua-standard-catalog.md)
for the current runtime policy. The generated metadata catalog remains future
tooling work.

The generated catalog will be useful for profile-specific documentation and
LuaLS output, but it should not open libraries, alter `Sandbox.Untrusted`, or
inspect a live Lua state. Runtime library selection remains a `Sandbox` policy
choice.

## Goals

- Describe Lua 5.4 or 5.5 standard APIs such as `base`, `math`, `string`, `table`,
  `utf8`, and `coroutine`.
- Keep `debug`, `io`, `os`, and package-loading APIs clearly separated because
  they have sandbox implications.
- Represent PopLua's safe profile: selected base helpers plus native `math`,
  `string`, `table`, and `utf8`.
- Project standard APIs into generated docs and LuaLS definitions when a future
  sandbox profile says they are available.
- Reuse the canonical manifest/type model instead of creating a second tooling
  pipeline.

## Manifest Shape To Evaluate

The existing design already reserves room for standard libraries:

```text
LuaApiManifest
  Libs[]
```

Likely stable IDs:

```text
standard:base.assert
standard:math.abs
standard:string.gsub
standard:table.insert
```

Likely source metadata:

```json
{ "sourceKind": "standard-library", "library": "math", "luaVersion": "5.4" }
```

The current manifest schema does not expose this yet. Adding it should be a
schema-versioned change with tests for JSON, LuaLS, Markdown, and profile
filtering.

## Non-Goals

- Runtime introspection of Lua globals.
- Changing which libraries are opened by a runtime sandbox.
- Modeling host-provided modules as standard libraries.
- Shipping sandbox profiles as runtime policy objects.

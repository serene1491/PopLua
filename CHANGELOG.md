# Changelog

All notable changes to PopLua will be documented in this file.

PopLua is currently pre-1.0. Public APIs may change before the first stable
release.

## [Unreleased]

## [1.0.0-rc.1]

### Added

- Added runtime selection for native Lua 5.4 and Lua 5.5, with automatic
  preference for 5.5 and an explicit `POPLUA_LUA_VERSION` override.
- Added `[Table]` copy-based generated return DTOs, including nested DTOs and
  bounded list output without exposing raw Lua table handles.
- Added `[Field("name")]` for explicit generated descriptor field names;
  unannotated fields retain the existing snake_case convention.
- Added nullable scalar descriptor fields; omitted or nil fields retain their
  CLR initializer while supplied values use the normal strict conversion.

### Changed

- Organized the public API by responsibility (`Binding`, `Context`,
  `Diagnostics`, `Exceptions`, `Marshaling`, `Runtime`, and `Sandboxing`) and
  removed redundant `Lua` prefixes from types.
- Renamed the runtime entry points to `Engine`, `EngineBuilder`, and `Session`;
  the sandbox remains `PopLua.Sandboxing.Sandbox`.
- Updated native interop for Lua 5.5's state seed, registry index, and selected
  standard-library opening API.
- Generated synchronous binding failures retain their managed exception as the
  nested cause of `ScriptException`, allowing hosts to classify platform
  defects without parsing error text.
- Module registration now includes userdata reached through other userdata
  methods and properties, so nested returned values always have their generated
  metatables.
- Unsupported or duplicate public `[Table]` fields now produce generator
  diagnostics instead of being omitted from generated Lua output.
- Added deterministic coverage for native library candidate names and
  actionable missing-library errors.
- Added Linux x64 NativeAOT publish and execution to the package-consumer release
  gate.
- Session completion is now published only after execution state is cleared,
  making immediate reuse deterministic after async success, failure, or cancellation.
- Closed and synchronized the RC roadmap, maturity matrix, platform claim, and
  native dependency documentation.

## [0.1.0-preview.6]

### Added

- Added generated input conversion for dense `IReadOnlyList<string>`,
  `IList<string>`, and `List<string>` values in Lua-exposed signatures and
  descriptor fields.
- Added array element metadata to generated JSON manifests, LuaLS definitions,
  and Markdown API documentation.

### Changed

- Generated descriptor tables now reject unknown fields instead of silently
  ignoring misspelled options.
- Generated descriptor and string lists now require dense one-based Lua arrays
  and reject named, sparse, or mixed table shapes.
- Consolidated generated list validation through one AOT-friendly binding path,
  reducing validation code hosts previously had to repeat around table options.

### Notes

- Descriptor conversion remains generated, copy-based, and input-only.
- Primitive descriptor lists support strings in this preview. Other primitive
  list types remain unsupported until their product need and conversion rules
  are clear.

## [0.1.0-preview.5]

### Added

- Added explicit `activeTime` and `wallTime` quota settings to replace the
  ambiguous preview `time` quota.
- Added per-function async suspended-time policy with
  `[Fn(Async = true, PauseTime = ...)]`.
- Added source-generated descriptor table conversion for structured Lua table
  parameters rooted in exposed `[Fn]` signatures.
- Added generated manifest, LuaLS, and Markdown metadata for descriptor/table
  DTO shapes.
- Added a descriptor example and a generated `ctx` module example backed by
  `Services`.
- Added computed module properties through `[Prop]` methods, including
  `[Context]` support for context-like APIs such as `ctx.author`.
- Added sandbox profile and Lua standard-library catalog documentation.
- Added maturity notes for runtime, binding, data interop, sandbox, tooling,
  packaging, and unsupported areas.
- Added a compatibility policy covering preview/stable SemVer, C# public APIs,
  Lua-facing generated APIs, manifest schema compatibility, and diagnostics.
- Added path-aware descriptor diagnostics for missing required fields and wrong
  nested field types.
- Added benchmark harness coverage for descriptor conversion, mixed varargs,
  and computed module property access.

### Changed

- Removed the old public `Quota(time: ...)` API in favor of explicit
  `activeTime` and `wallTime` limits.
- Clarified quota diagnostics and docs so active-time, wall-time, instruction,
  memory, and call-depth quota failures are distinct.
- Removed public `Session.SetGlobal(...)` and `SetGlobalNil(...)` APIs so
  embedders do not construct public Lua API surfaces through dynamic global
  mutation.
- Re-centered recommended host API design on `[Module]`, `[Userdata]`,
  `[Context]`, `Services`, and generated descriptor types.
- Improved generated Markdown API docs with nested navigation, grouped
  module/userdata/descriptor sections, compact typed signatures, and less
  generic policy noise.
- Pretty-printed generated `poplua.api.json` with deterministic ordering.
- Generated manifest, LuaLS, and Markdown artifacts now emit by default for
  generated binding projects; individual outputs can be disabled with the
  matching `PopLuaGenerate...` property.
- Improved LuaLS and manifest type metadata for nullable generated values.
- Clarified that context-like root APIs such as `ctx` should be generated
  modules, while `ScriptContext` remains the C# execution context for generated
  bindings.
- Removed the redundant `GeneratingDocs` example now that generated-binding
  examples emit API artifacts by default.
- Finalized `Value[]` vararg behavior: primitive values round-trip, while
  table/function/userdata values are accepted as opaque `ValueKind` entries
  for inspection and are not typed/durable handles.
- Closed the `Task` / `Task<T>` async binding decision for `1.0`:
  generated async bindings use `ValueTask` / `ValueTask<T>`, and `Task` /
  `Task<T>` remain unsupported diagnostics.
- Closed the descriptor return/table reference decision for `1.0`: descriptor
  DTOs are generated, copy-based, and input-only; generated table writers and
  public Lua table references remain post-1.0 candidates.
- Documented the synchronous generated callback / Lua `pcall` caveat as a
  stable `1.0` limitation: PopLua fails the whole `Result`, but Lua `pcall`
  may report success for the protected synchronous callback.
- Closed several 1.0 scope decisions: generated standard-library catalog
  metadata, sandbox-profile filtered outputs, async `require`, filesystem
  conventions, and `poplua-modhost` are post-1.0 / opt-in-layer work rather
  than 1.0 blockers.
- Closed the native Lua packaging strategy for `1.0`: PopLua keeps Lua 5.4 or 5.5 as
  a documented external dependency for the stable release, while bundled or
  RID-specific native packages remain post-1.0 deployment work.
- Reworked the `TODO.md` 1.0 checklist so release blockers, decisions,
  post-1.0 candidates, non-goals, and operational validation work are separated.
- Cleaned roadmap and maturity notes to remove completed descriptor/docs
  hardening items, classify descriptor returns/table references as post-1.0
  candidates, and focus 1.0 work on packaging, validation, and
  performance.
- Added generator/runtime tests for unsupported `Task` / `Task<T>` async
  returns and descriptor edge cases.
- Extended example validation to parse generated `poplua.api.json` manifests
  with `python3 -m json.tool`.

### Notes

- `PauseTime` defaults to `true` for async generated functions. It pauses
  `activeTime` only while the operation is actually suspended; `wallTime` always
  continues to run.
- Arbitrary synchronous C# binding work cannot be interrupted until PopLua
  reaches a safe runtime checkpoint.
- PopLua still mutates Lua globals internally to install generated modules,
  controlled `require`, selected native Lua libraries, and sandbox setup. That
  is PopLua-owned runtime bootstrapping, not a public embedder API.
- Descriptor conversion is intentionally copy-based and generated. This preview
  does not add public Lua table references or descriptor return values.

## [0.1.0-preview.4]

### Added

- Added per-session global/value injection through `Session.SetGlobal(...)`
  and `SetGlobalNil(...)`.
- Added support for injecting supported primitive values, `Value`, nil, and
  source-generated `[Userdata]` values as session globals.
- Added `FunctionRef` for session-owned Lua function/callback references.
- Added generated binding support for accepting `FunctionRef` parameters.
- Added LuaLS/docs metadata so `FunctionRef` is represented as Lua
  `function`.
- Added diagnostic `PLUA011` for userdata instance methods that incorrectly
  declare `Value self`.
- Added examples for session globals and callbacks.

### Changed

- Clarified Lua `.` vs `:` call behavior in docs.
- Clarified that userdata instance methods receive the C# instance as `this`;
  PopLua consumes the Lua userdata receiver internally.
- Clarified that ordinary `Value` parameters remain supported, but
  `Value self` is not a valid userdata receiver pattern.
- Clarified callback/session lifetime rules: Lua callbacks are session-owned
  and are not durable across session disposal.
- Clarified recommended host patterns for durable UI/event interactions:
  store host-side IDs, routes, and payloads instead of retaining Lua closures
  after a session ends.
- Documented that injected globals are normal Lua globals and may be overwritten
  by Lua within the session.

### Notes

- `SetGlobal` is session-scoped and blocked during active execution.
- `SetGlobal` does not create readonly/protected globals; hosts should expose
  safe userdata/API surfaces instead.
- `FunctionRef` is not a durable callback handle and must not be used after
  its owning `Session` is disposed.
- Durable callbacks, closure serialization, protected globals, table global
  injection, and filesystem/package callback models remain out of scope for
  this preview.

## [0.1.0-preview.3]

### Changed

- Replaced the generated `PopLuaModules.RegisterAll` aggregate registration
  pattern with source-visible generic module registration such as
  `Module<T>()` and `Modules<T1, T2>()`.
- Updated tests, benchmarks, examples, templates, scripts, and current docs to
  avoid generated aggregate symbols in user-authored C# code.

### Notes

- This preview keeps source-generated, AOT-friendly registration and does not
  add runtime reflection discovery.
- Native Lua binaries are still not packaged.

## [0.1.0-preview.2]

### Added

- Production usage checklist for host applications.
- Split-project host example showing a runtime-only app, an unsafe-enabled
  generated binding library, diagnostics, metrics, services, sandboxing, and an
  explicit `log` module pattern.
- Controlled loading design note for host-managed script/module resolution.
- Script-author guide for host modules, generated docs, LuaLS definitions,
  output, sandbox limits, errors, and reusable-code expectations.
- Design notes for future standard Lua catalog and sandbox-profile tooling.
- NuGet package icon metadata using the repository SVG logo as the source asset.
- Controlled host-managed `require` with strict module names, synchronous
  resolver callbacks, named chunks, per-session module value caching, and no
  filesystem search by default.
- Opt-in native Lua standard-library selection for custom sandboxes, including
  a conservative safe profile.
- Separate `PopLua.Templates` package source with `poplua`,
  `poplua-bindings`, and `poplua-host` templates.
- Async userdata instance methods through `[Fn(Async = true)]`.
- Async userdata example.

### Changed

- Public XML documentation, manifest documentation, LuaLS definitions, and
  generated Markdown documentation were hardened as product surface.
- Benchmark documentation now distinguishes dry smoke checks from short-run
  baseline numbers.
- Package metadata and release notes now target the second preview.
- Roadmap now focuses on remaining work instead of completed preview
  milestones.

### Performance

- Added async userdata benchmark coverage.
- Cached PopLua's generated async wrapper factory per Lua state, reducing the
  completed async userdata method short-run benchmark from about `183.971 ms` to
  about `27.294 ms` per 10k calls on the benchmark machine.

### Known Limitations

- Host applications must provide a Lua 5.4 or 5.5 native library discoverable as
  `lua5.4`.
- Native Lua binaries are not packaged yet; this is a deployment convenience
  gap, not an expected runtime performance gap.
- Controlled loading does not include async resolvers, package managers,
  automatic filesystem search, or mod manifests yet.
- Host-controlled script output should be exposed through generated modules.
- Synchronous generated managed callback failures have a documented Lua `pcall`
  caveat.
- Standard Lua library catalog metadata and sandbox-profile tooling outputs are
  future work.
- Userdata-heavy workloads still need benchmark-guided optimization.
- NativeAOT validation beyond linux-x64 remains future validation work.

## [0.1.0-preview.1]

Initial preview release.

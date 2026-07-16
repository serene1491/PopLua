# PopLua 1.0 RC1 Closure

The `1.0.0-rc.1` scope is closed. This file records the release gate; it is not
an open backlog.

## Release Gate

- [x] Public APIs are organized by responsibility and namespaces follow source
  directories.
- [x] `PopLua.Sandboxing.Sandbox` remains the immutable sandbox policy.
- [x] Lua 5.4 and Lua 5.5 runtime selection, supported library names, version
  verification, override behavior, and actionable load failures are covered.
- [x] Linux x64 is the validated RC platform. CI covers Ubuntu with Lua 5.4 and
  Arch Linux with Lua 5.5.
- [x] Linux x64 NativeAOT publish and execution are validated for runtime-only
  and generated-binding consumers.
- [x] Runtime, generator, sandbox, quota, async, controlled-loading, userdata,
  callback, descriptor, and copy-based table-result behavior is tested.
- [x] Unsupported and duplicate `[Table]` output fields fail at generation
  instead of disappearing from the public Lua contract.
- [x] Runtime and template packages are inspected by the release gate.
- [x] All examples and templates build; representative examples and installed
  templates run from locally packed packages.
- [x] Generated manifests are validated as JSON.
- [x] First-contact, maturity, compatibility, native dependency, release, and
  roadmap documentation agrees with the RC implementation.
- [x] Package versions and install snippets use `1.0.0-rc.1`.
- [x] The full `./scripts/check.sh` release gate passes on Lua 5.5.

## Closed Scope Decisions

- Lua 5.4 and 5.5 remain external native dependencies; native binaries are not
  bundled.
- `ValueTask` and `ValueTask<T>` are the generated async shapes. `Task` and
  `Task<T>` are rejected.
- `[Table]` provides generated, bounded, copy-based Lua table results. Raw
  session-owned table handles are not public.
- Generated modules and userdata remain source-generated and AOT-friendly.
- Runtime reflection discovery, dynamic global API construction, filesystem
  package search, durable Lua closures, raw native interop, and a mod framework
  are outside PopLua 1.0.
- Windows and macOS are not claimed as validated RC platforms. Expanding the
  platform matrix requires a later release with native-library CI on each
  platform.

Future proposals belong in GitHub issues after the RC is released. There is no
remaining repository TODO committed for `1.0.0-rc.1`.

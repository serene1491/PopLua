# Roadmap

## 1.0 RC1

The `1.0.0-rc.1` roadmap is complete.

The release includes:

- isolated `Engine` and `Session` execution;
- Lua 5.4 and Lua 5.5 native runtime selection;
- immutable `PopLua.Sandboxing.Sandbox` policies and bounded quotas;
- generated modules, userdata, descriptors, async functions, and controlled
  host-managed `require`;
- generated copy-based `[Table]` results without raw Lua table handles;
- manifest, LuaLS, and Markdown generation;
- runtime and host templates;
- package, example, Linux x64 NativeAOT, and clean-consumer validation.

Linux x64 is the validated RC platform. Ubuntu CI exercises Lua 5.4 and Arch
Linux CI exercises Lua 5.5. Windows and macOS are not part of the RC validation
claim.

## Stable 1.0

The stable release has no precommitted feature backlog. RC feedback may produce
concrete compatibility or correctness issues; only verified issues should block
stable 1.0.

Ideas such as bundled native binaries, more platform targets, filtered tooling,
async module resolvers, or mod-host conventions are intentionally not roadmap
commitments. They should become scoped GitHub issues only when a real host need
and validation strategy exist.

## Non-goals

- Runtime reflection module discovery.
- Public dynamic Lua/global API construction.
- Durable Lua callback handles or closure serialization.
- Public raw Lua stack/native interop APIs.
- Filesystem package search by default.
- A mod framework.
- `dynamic` or expression-compilation binding dispatch.
- Luau, LuaJIT, or a third runtime family.

# Native Lua Decision

This is a maintainer-oriented design note. Users normally only need to know that
PopLua calls Lua 5.4 or 5.5 through its internal native bridge and the host must
provide a compatible native library available under a supported platform library name.

## Decision

PopLua does not use KeraLua as its core runtime bridge. It uses a small internal
P/Invoke layer over Lua 5.4 or 5.5.

## Short Answer

KeraLua is valuable, but not for PopLua's hot path.

It is good for:

- learning from an established binding;
- quick experiments;
- checking Lua API behavior;
- possible future packaging ideas.

It is not ideal as PopLua's main internal layer because PopLua needs:

- zero-copy UTF-8 reads through `lua_tolstring`;
- exact control over stack operations;
- exact control over debug hooks for quotas;
- generated C callback functions;
- AOT-friendly, minimal interop with no extra public concepts.

## Reliability Tradeoff

KeraLua can feel safer initially because it hides native details. The cost is
that PopLua would either lose zero-copy behavior or bypass KeraLua for critical
paths anyway.

The chosen path is:

1. Keep the native bridge tiny.
2. Keep it internal.
3. Test every exposed behavior through public APIs.
4. Document the Lua 5.4 or 5.5 native library requirement.

## Current Native Requirement

PopLua keeps Lua as a documented external dependency rather than bundling
native binaries in the runtime package.

The RC validates Linux x64 with Lua 5.4 on Ubuntu and Lua 5.5 on Arch Linux.
It validates resolver names and actionable missing-library messages. Windows
and macOS are not part of the RC support claim.

## Packaging And Performance

Bundling Lua is primarily a deployment convenience, not a runtime performance
optimization for PopLua's current architecture.

Shipping a known Lua 5.4 or 5.5 shared library would make installation easier, but it
would not meaningfully change:

- Lua VM execution speed;
- Lua-to-C# transition overhead;
- generated callback dispatch cost;
- userdata wrapper or `GCHandle` allocation pressure;
- async bridge scheduling cost.

Static linking may change deployment shape and library resolution behavior, but
PopLua still crosses the same Lua C API boundary and generated callback path.
Any startup difference is expected to be small compared with the deployment and
maintenance cost of RID-specific native assets.

Native packaging should therefore be evaluated later for install experience and
operational predictability, not prioritized as a performance task. It also needs
dedicated CI, licensing checks, AOT validation, and release-process design.

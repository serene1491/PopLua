# Script Author Guide

This page is for people writing Lua scripts that run inside a PopLua host.
Host applications decide which APIs are available.

## Calling Host APIs

PopLua hosts expose C# APIs as Lua modules:

```lua
local name = host.identity()
log.info("loaded for " .. name)
```

Module names, functions, userdata types, and capabilities are host-defined. Use
the generated `poplua-api.md` for readable API documentation and
`poplua.d.lua` for LuaLS/LuaCATS editor completion when the host provides them.
PopLua hosts should expose stable public Lua APIs through generated modules,
userdata, and descriptor tables so those APIs appear in docs and editor tooling.

## Call Style

Use `.` for module APIs:

```lua
log.info("hello")
api.get_user("42")
```

Use `:` for userdata instance methods:

```lua
local player = players.get("42")
player:rename("Pop")
```

In Lua, `player:rename("Pop")` passes `player` as the receiver. PopLua uses
that receiver internally to find the C# userdata instance. The C# method
receives the object through `this`, not through a Lua `self` parameter.

The equivalent manual receiver form, `player.rename(player, "Pop")`, follows
normal Lua rules and also works for userdata methods, but PopLua examples and
generated docs use `:` because it is clearer. Module functions are ordinary
functions stored on module tables, so call them with `.` unless your host
documents a different convention.

Async host APIs also look like ordinary Lua calls. PopLua may suspend the
current script while a generated async module function or userdata method waits
for host work, then resume it with the returned value:

```lua
local player = players.find("Pop")
local name = player:get_name_async()
```

Async task faults are Lua errors when the host exposes `pcall`; host
cancellation remains a terminal execution failure. Synchronous host binding
failures may still fail the whole PopLua execution even when wrapped in Lua
`pcall`, so host APIs that need catchable script-side errors should expose an
async generated function or an explicit status result.

## Output

For user-authored scripts, prefer the host's explicit output module instead of
global `print`:

```lua
log.info("loaded")
log.warn("missing optional setting")
log.error("failed")
```

This lets the host attach identity, route messages to its own logs, and keep
output available even when standard Lua libraries are not opened.

## Sandboxes

Untrusted PopLua sessions start without standard Lua libraries unless the host
opts into a profile. A common safe profile provides selected base helpers plus
Lua's native `math`, `string`, `table`, and `utf8` libraries. Do not assume
globals such as `print`, `require`, `package`, `io`, `os`, or `debug` exist
unless your host documents them.

Capabilities are host-defined labels. If an API is not available, the host may
have omitted the module or denied the capability for the current script.

## Errors

Hosts should name scripts and modules. When they do, runtime errors and
tracebacks include names such as:

```text
plugin:on_start.lua:17: attempt to index a nil value
```

Report the chunk name, line number, and traceback to the host when debugging.

## Session-Owned Values

Lua values, tables, functions, closures, and userdata belong to the
`Session` that created them. Hosts should expose context-like roots as
generated modules, such as `ctx`, backed by per-session services:

```lua
ctx.reply("hello")
```

`ctx` in this example is a host-defined module, not PopLua's C# `ScriptContext`.
The C# `ScriptContext` is only the per-call execution context available to
generated bindings.

Hosts may also accept callbacks and keep a session-owned function reference:

```lua
-- Long-lived session only.
button.on_click(function(ctx)
    ctx:reply("clicked")
end)
```

That callback is a normal Lua closure. It can be called only while the owning
session is alive and the host still holds the reference.

For durable UI events, scheduled work, Discord buttons/selects/modals, or other
interactions that may arrive after a session ends, do not rely on stored Lua
closures. Store host-side IDs and routing data instead:

```lua
ui.button({
    id = "delete-message",
    label = "Delete",
    data = { message_id = ctx.message_id }
})
```

Then let the host start a fresh script execution with explicit context when the
event arrives.

## Structured Tables

Hosts may accept structured Lua tables as descriptor values:

```lua
ui.select("choice", {
  placeholder = "Choose an option",
  tags = { "compact", "searchable" },
  options = {
    { label = "Option A", value = "a" },
    { label = "Option B", value = "b" }
  }
})
```

When the host uses generated descriptor types, these shapes appear in
`poplua-api.md` and `poplua.d.lua`. Descriptor tables are copied into C# data
during the call. They are not durable Lua table references, and descriptor DTOs
are input-only in PopLua `1.0`. Field names are strict. Descriptor-object lists
and string lists must use dense one-based arrays rather than named or sparse
tables.

## Reusable Code

Hosts can opt into PopLua-controlled `require` for approved reusable Lua code:

```lua
local util = require("util")
log.info(util.message())
```

PopLua validates module names before asking the host resolver. The default
valid shape is a dot-separated identifier such as `util`, `game.player`, or
`utils.math`. Names such as `../x`, `/x`, `x.lua`, `x/../y`, and empty strings
are rejected.

Loaded module return values are cached per session. If a module returns `nil`,
PopLua caches `true`, matching Lua's normal `require` convention. Failed loads
are not cached.

Common failure messages look like `module not found: util`,
`invalid module name: ../util`, or `cyclic module load: a -> b -> a`.

The host still owns storage and approval. PopLua does not search the
filesystem, `package.path`, or `package.cpath` by default, and it does not
provide a package manager. If your host does not enable controlled loading,
`require` may be unavailable.

When Lua's `pcall` and `error` are available, missing, invalid, cyclic, and
runtime module failures behave like Lua errors and can be caught by `pcall`.
In untrusted sandboxes that do not expose those standard functions, failures
stop the PopLua execution result through the normal `Result.Error` path.

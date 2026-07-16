# Lua Standard Library Catalog

PopLua exposes native Lua 5.4 or 5.5 standard libraries only when the sandbox asks
for them. `Sandbox.Untrusted` opens none of these libraries by default.

| Library | `Library` flag | Safe profile | Trusted profile | Notes |
|---|---|---:|---:|---|
| Selected base functions | `SafeBase` | yes | no, superseded by `FullBase` | Opens native base then removes broad helpers. |
| Full base | `FullBase` | no | yes | Includes `load`, `dofile`, `loadfile`, `collectgarbage`, `print`, raw helpers, and metatable helpers. |
| `coroutine` | `Coroutine` | no | yes | Native coroutine library. PopLua's async bridge is separate. |
| `math` | `Math` | yes | yes | Native Lua math library. |
| `string` | `String` | yes | yes | Native Lua string library. |
| `table` | `Table` | yes | yes | Native Lua table library. |
| `utf8` | `Utf8` | yes | yes | Native Lua UTF-8 library. |
| `package` | `Package` | no | yes | Native package loading/search behavior. Separate from PopLua-controlled `require`. |
| `io` | `Io` | no | yes | Native file I/O library. Avoid for untrusted scripts. |
| `os` | `Os` | no | yes | Native OS library. Avoid for untrusted scripts. |
| `debug` | `Debug` | no | yes | Native debug library. Avoid for untrusted scripts. |

## Safe Profile

`AllowSafeLibs()` is equivalent to:

```csharp
AllowLibs(Library.Safe)
```

`Library.Safe` currently means:

```text
SafeBase | Math | String | Table | Utf8
```

`SafeBase` keeps:

```text
assert, error, ipairs, pairs, pcall, select, tonumber, tostring, type
```

and removes broad base globals such as:

```text
collectgarbage, dofile, getmetatable, load, loadfile, print,
rawequal, rawget, rawlen, rawset, setmetatable, warn, xpcall
```

Host applications should expose output/logging through generated host modules
rather than Lua `print`.

## Full Trusted Profile

`Sandbox.Trusted` opens `Library.All`, which includes full base, package, I/O,
OS, and debug libraries. Use it only for host-owned scripts.

## Future Metadata

This page is a manual catalog of PopLua's runtime policy. Generated
standard-library metadata for filtered LuaLS/docs profile outputs remains
future tooling work.

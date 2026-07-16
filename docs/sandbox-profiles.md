# Sandbox Profiles

PopLua separates Lua execution policy from the host API surface. Sandboxes
control native Lua libraries, PopLua capabilities, and quotas. Generated
modules, controlled `require`, and host services are configured separately.

## Built-In Profiles

| Profile | Libraries | Capabilities | Quotas | Use |
|---|---|---|---|---|
| `Sandbox.Untrusted` | none | none | default instruction, active-time, wall-time, and call-depth quotas | User-authored scripts by default. |
| `Sandbox.Trusted` | all native Lua 5.4 or 5.5 libraries | all capabilities | none | Host-owned/internal scripts only. |

`Sandbox.Untrusted` is deliberately closed. It does not expose `math`,
`string`, `table`, `utf8`, `pcall`, `print`, `io`, `os`, `package`, or
`debug` unless the host opts in through a custom sandbox.

`Sandbox.Trusted` opens Lua's full native standard library set and allows every
PopLua capability. Do not use it for untrusted user-authored scripts.

## Custom Sandboxes

Build custom policies with `Sandbox.Build(...)`:

```csharp
var sandbox = Sandbox.Build(b => b
    .AllowSafeLibs()
    .Allow("inventory.read")
    .Quota(
        instructions: 100_000,
        activeTime: TimeSpan.FromSeconds(1),
        wallTime: TimeSpan.FromSeconds(30),
        callDepth: 128));
```

`Sandbox.Build` starts empty. Add quotas explicitly when the custom policy is
for untrusted scripts.

## Capabilities

Generated modules can declare a capability:

```csharp
[Module("inventory", Cap = "inventory.read")]
public partial class InventoryModule
{
}
```

The module is registered only when the session sandbox allows that capability.
Capabilities are host-defined strings; they do not grant file, network,
process, or environment access unless host APIs perform those operations.

## Controlled `require`

PopLua-controlled `require` is installed only when the host configures a
resolver with `EngineBuilder.Require(...)`. It maps strict module names to
host-approved `Chunk` values. It does not use `package.path`, `package.cpath`,
or native Lua filesystem/package search.

## Native Lua Libraries

Use `AllowSafeLibs()` for the conservative profile:

```csharp
var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
```

Use `AllowLibs(...)` when the host needs a specific set:

```csharp
var sandbox = Sandbox.Build(b => b.AllowLibs(
    Library.Math | Library.String | Library.Table | Library.Utf8));
```

Avoid `Library.Io`, `Library.Os`, `Library.Package`, `Library.Debug`, and
`Library.FullBase` for untrusted scripts unless the host deliberately accepts
the native Lua behavior those libraries expose.

## Public API Surface

Stable Lua-facing APIs should be source-generated with `[Module]`,
`[Userdata]`, and descriptor types.

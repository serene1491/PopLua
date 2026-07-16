# Sandbox Profile Tooling Design

This is a maintainer design note for future filtered documentation and LuaLS
outputs. Manual sandbox profile documentation exists in
[Sandbox Profiles](sandbox-profiles.md); generated filtered profile artifacts
remain future tooling work.

## Goal

Hosts should eventually be able to produce editor and documentation artifacts
that match an intended script environment:

```text
poplua.untrusted.d.lua
poplua.host.d.lua
poplua.trusted.d.lua
```

The output should help script authors see only the APIs available to a given
profile.

## Inputs

Profiles should be explicit build/tooling metadata, not serialized runtime
`Sandbox` objects. Runtime sandboxes can include host-specific decisions,
services, and policies that do not belong in generated files.

Potential inputs:

- manifest modules and userdata;
- required capabilities from `[Module(Cap = "...")]`;
- a future static standard Lua catalog aligned with `Library`;
- build properties or item metadata that name profiles and allowed
  capabilities/libraries.

## Projection Rules

- Include modules whose required capability is allowed by the profile.
- Include modules without a capability unless the profile explicitly excludes
  uncategorized APIs.
- Include userdata referenced by included APIs.
- Include standard libraries selected by the profile, such as PopLua's safe
  profile or a host-selected subset.
- Preserve capability notes in Markdown output.
- Prefer separate files per profile over mutating the base manifest.

Profiles are tooling projections. They must not imply that a build-time output
serializes or exactly reproduces a runtime `Sandbox` instance.

## Build Shape To Evaluate

Profile outputs should be opt-in build artifacts next to the existing manifest
outputs, for example:

```xml
<PopLuaGenerateSandboxProfileDefinitions>true</PopLuaGenerateSandboxProfileDefinitions>
<PopLuaSandboxProfiles>untrusted;host</PopLuaSandboxProfiles>
```

The exact property names should be chosen when the feature is implemented. The
important boundary is that the canonical manifest remains unfiltered, while
profile files are derived projections for editor and documentation workflows.

## Non-Goals

- Changing runtime sandbox behavior.
- Inferring profiles by executing Lua.
- Treating `Caps.*` as operating-system permissions.
- Hiding APIs from the unfiltered canonical manifest.

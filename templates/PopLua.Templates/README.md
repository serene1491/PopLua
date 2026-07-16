# PopLua.Templates

Starter templates for PopLua script-hosting projects.

Install:

```bash
dotnet new install PopLua.Templates::1.0.0-rc.1
```

Templates:

- `poplua`: minimal runtime-only console runner.
- `poplua-bindings`: unsafe-enabled class library for generated modules and userdata.
- `poplua-host`: split host with runtime-only app, generated bindings library,
  safe standard-library profile, controlled `require`, logging, diagnostics, and
  named script chunks.

`poplua-host` keeps `Program.cs` small and places host-side runtime setup,
script catalog, diagnostics, and log capture under the app project's `Scripting`
folder. The generated binding project remains separate and is the only project
that enables unsafe blocks.

The templates reference the separate `PopLua` runtime package. They do not
bundle native Lua binaries; generated projects still need a compatible Lua 5.4 or 5.5
native library available under a supported platform library name at run time.

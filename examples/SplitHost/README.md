# Split Host Example

This example shows the recommended production shape when generated bindings are
part of a larger host application.

```text
SplitHost.App/
  Program.cs                 small runtime-only entry point, no unsafe blocks
  Scripting/                 runtime setup, catalog, diagnostics, log capture
SplitHost.Scripting/
  Modules/                   generated Lua modules
  Userdata/                  generated userdata
  SplitHost.Scripting.csproj unsafe enabled only here
```

The app project owns `Engine`, sessions, sandbox policy, diagnostics,
identity, services, and metrics. The scripting project owns `[Module]` and
`[Userdata]` declarations and exposes a small source-visible registration
wrapper for its generated module types.

`Program.cs` stays intentionally small. The app-side `Scripting/` folder keeps
the script catalog, host diagnostics, log capture, and execution helper separate
from the generated binding project.

The sandbox opts into PopLua's safe native standard-library profile, which
provides selected base helpers plus Lua's native `math`, `string`, `table`, and
`utf8` libraries without opening `io`, `os`, `package`, or `debug`.

The example also demonstrates host-controlled script output through an explicit
`log` module:

```lua
log.info("loaded")
log.warn("score changed")
```

That pattern works in `Sandbox.Untrusted`, does not require standard Lua
libraries, and lets the host route messages to its own storage or logging
system.

The sample script uses a real multi-file flow:

```text
SplitHost.App/Scripts/
  main.lua
  util.lua
```

The app loads those approved files into a small host-owned catalog and exposes
that catalog through `EngineBuilder.Require`. PopLua validates module names
and runs the loaded chunk on the active session; it does not search the
filesystem, `package.path`, or `package.cpath` by default.

Run it with:

```bash
dotnet run --project examples/SplitHost/SplitHost.App/SplitHost.App.csproj
```

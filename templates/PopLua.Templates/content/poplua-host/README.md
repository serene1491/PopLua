# PopLua Host

This template shows a small production-oriented PopLua host:

- `PopLuaHost.App` owns runtime creation, sandbox policy, script compilation,
  sessions, diagnostics, and execution.
- `PopLuaHost.Scripting` owns generated modules and userdata, so unsafe blocks
  are enabled only in that project.
- Scripts use an explicit `log` module and host-controlled `require`.
- `Program.cs` stays small; host-side runtime setup, script catalog,
  diagnostics, and log capture live under `PopLuaHost.App/Scripting`.

PopLua does not search the filesystem by default. The app loads approved Lua
files into its own catalog and the resolver returns named `Chunk` values.
`AllowSafeLibs()` exposes selected native Lua helpers such as `math`, `string`,
`table`, and `utf8`; it does not open `io`, `os`, `package`, or `debug`.

Use `Sandbox.Trusted` only for trusted host-owned scripts, not user-authored
scripts.

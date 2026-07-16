# PopLua Minimal Runner

This is a runtime-only console project. It does not need unsafe blocks because
it does not declare generated bindings.

The sample creates a `Engine`, starts from `Sandbox.Untrusted`, opts into
PopLua's safe native Lua library profile with `AllowSafeLibs()`, and runs a
named `Chunk` so errors and diagnostics can identify the script.

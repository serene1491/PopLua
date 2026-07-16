# PopLua Bindings

This project declares generated PopLua modules and userdata, so it enables
`AllowUnsafeBlocks`. Runtime-only app projects that consume this library do not
need unsafe blocks unless they declare their own generated bindings.

Build output includes optional API artifacts under `obj/.../poplua-api`:

- `poplua.api.json`
- `poplua.d.lua`
- `poplua-api.md`

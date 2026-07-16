# PopLua Diagnostics

PopLua generator diagnostics use the `PLUA` prefix. They are emitted at build
time by the source generator and should be treated as compile-time errors in
generated binding projects.

## Generator Diagnostics

| Code | Title | Meaning | Typical Fix |
|---|---|---|---|
| `PLUA001` | Lua function must be public | A `[Fn]` method is not public. | Make the method public or remove `[Fn]`. |
| `PLUA002` | Unsupported Lua marshaling type | A Lua-exposed parameter, return, property, field, descriptor member, or constant uses a type PopLua cannot marshal. | Use a supported primitive, `string`, `Value`, `FunctionRef`, generated userdata, supported descriptor type, or supported `ValueTask` shape. |
| `PLUA003` | Lua module must be partial | A `[Module]` type is not partial. | Add the `partial` modifier so generated code can attach registration support. |
| `PLUA004` | `ScriptContext` parameter must be first | A `[Context]` parameter appears after another parameter. | Move the `[Context] ScriptContext` parameter to the first parameter position. |
| `PLUA005` | Async Lua function has invalid return type | A `[Fn(Async = true)]` method does not return `ValueTask` or `ValueTask<T>`. `Task` and `Task<T>` are intentionally unsupported for generated async bindings. | Change the method to return `ValueTask` or `ValueTask<T>`, or remove `Async = true`. |
| `PLUA006` | Lua userdata must be partial | A `[Userdata]` type is not partial. | Add the `partial` modifier so generated code can attach userdata support. |
| `PLUA007` | `Value[]` array must be last | A `Value[]` variadic parameter is followed by another parameter. | Move the `Value[]` parameter to the end of the signature. |
| `PLUA008` | Duplicate Lua name | A module or userdata exposes the same Lua name more than once. | Rename one member with the attribute name argument or remove one exposed member. |
| `PLUA010` | Generated Lua bindings require unsafe blocks | A project declares generated Lua bindings without `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`. | Enable unsafe blocks in the generated binding project. Runtime-only projects do not need unsafe blocks. |
| `PLUA011` | Userdata receiver is supplied by PopLua | A userdata instance method declares `Value self`. | Remove the `self` parameter. PopLua consumes the Lua userdata receiver internally, and the C# method uses `this`. |
| `PLUA012` | `PauseTime` requires an async Lua function | A `[Fn]` method sets `PauseTime = true` without `Async = true`. | Add `Async = true` and return `ValueTask` / `ValueTask<T>`, or remove `PauseTime`. |

## Notes

- Diagnostics describe C# source declarations, not runtime Lua script failures.
- Runtime-only projects that create `Engine` and `Session` do not need
  unsafe blocks unless they also declare generated bindings.
- Generated binding projects should normally treat diagnostics as blocking.
- `Value` is valid for ordinary Lua values where supported, but it is not the
  userdata receiver parameter.

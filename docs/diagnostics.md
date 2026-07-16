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
| `PLUA006` | Lua userdata must be partial | A `[Userdata]` type is not partial. | Add the `partial` modifier so generated code can attach userdata support. |
| `PLUA007` | `Value[]` array must be last | A `Value[]` variadic parameter is followed by another parameter. | Move the `Value[]` parameter to the end of the signature. |
| `PLUA008` | Duplicate Lua name | A module or userdata exposes colliding Lua members that are not `[Fn]` overloads. | Rename one member with the attribute name argument or remove one exposed member. |
| `PLUA010` | Generated Lua bindings require unsafe blocks | A project declares generated Lua bindings without `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`. | Enable unsafe blocks in the generated binding project. Runtime-only projects do not need unsafe blocks. |
| `PLUA011` | Userdata receiver is supplied by PopLua | A userdata instance method declares `Value self`. | Remove the `self` parameter. PopLua consumes the Lua userdata receiver internally, and the C# method uses `this`. |
| `PLUA012` | `CountWait` requires an async Lua function | A `[Fn]` method sets `CountWait = true` but does not return `ValueTask` or `ValueTask<T>`. | Return `ValueTask` / `ValueTask<T>`, or remove `CountWait`. |
| `PLUA013` | Text coercion requires a string parameter | `[Text]` is attached to a parameter that is not `string`. | Change the parameter to `string`, or remove `[Text]` and use the strict marshaler for its real type. |
| `PLUA014` | Unsupported injected context type | `[Context]` is attached to a type other than `ScriptContext` or `CancellationToken`. | Use one of the supported injected types or pass the value as a normal Lua argument. |
| `PLUA015` | Lua overloads must be distinguishable | Two `[Fn]` overloads have the same Lua-visible arity and parameter kinds. | Change the Lua-visible parameter shape or expose one method under another name. CLR-only width differences such as `int`/`long` do not distinguish overloads. |
| `PLUA016` | Lua overloads must share one execution mode | A Lua overload group mixes ordinary return values with `ValueTask` / `ValueTask<T>`. | Make every overload synchronous or every overload asynchronous. |

## Notes

- Diagnostics describe C# source declarations, not runtime Lua script failures.
- Runtime-only projects that create `Engine` and `Session` do not need
  unsafe blocks unless they also declare generated bindings.
- Generated binding projects should normally treat diagnostics as blocking.
- `Value` is valid for ordinary Lua values where supported, but it is not the
  userdata receiver parameter.

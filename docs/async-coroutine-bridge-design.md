# Async Coroutine Bridge Design

This document defines the implementation architecture for PopLua's async
coroutine bridge. The module-function and userdata-method bridge described here
is implemented for `ValueTask` and `ValueTask<T>` generated bindings.

## Current Runtime Facts

PopLua runs `Run` and `Call` executions on coroutine threads owned by
`Session`.

- `Engine.Run(...)` creates a session, delegates to `Session.Run(...)`,
  and awaits the returned `ValueTask`.
- `Session.Run(Chunk)` loads the chunk on a coroutine thread, executes it
  with `lua_resume`, and resumes again after supported async yields.
- `Session.Call(...)` pushes a global function and arguments onto a
  coroutine thread and uses the same resume loop.
- Generated module functions and userdata callbacks are unmanaged C callbacks
  generated with `[UnmanagedCallersOnly]`.
- `Marshaller.Context(state)` reads the current callback context from a
  thread-static `Session.CurrentContext`.
- Synchronous managed callback failures are captured by `Marshaller.Error(...)`,
  stored on the owning `Session`, and surfaced after `lua_pcallk` returns as a
  failed `Result`. Their managed exception is retained as the nested cause;
  the Lua-facing message remains unchanged.
- Because managed failures are not raised with `lua_error`, Lua `pcall` can
  report success for synchronous callback failures even when PopLua later fails
  the whole session result.
- Async module-function faults are raised by the Lua wrapper after resume and
  are catchable by Lua `pcall`.
- Host cancellation during suspension is terminal and is not catchable by Lua
  `pcall`.
- Quotas use a Lua debug hook installed on the active coroutine thread.
- A caller-provided cancellation token also installs the debug hook so
  cancellation can interrupt active Lua execution.
- Instruction counts and elapsed time are updated on `ExecutionState`.
- `Sandbox.MaxInstructions`, `Sandbox.MaxActiveTime`, and `Sandbox.MaxWallTime`
  are enforced by the debug hook while Lua is active.
- `Sandbox.MaxCallDepth` is enforced by debug call/return hooks on the active
  coroutine thread.
- `Sandbox.MaxHeapBytes` is enforced by PopLua's internal Lua allocator.
- `GcThresholdBytes` is observed by the allocator and debug hook; the hook
  triggers Lua collection after tracked heap usage crosses the threshold.
- The generator supports `[Fn(Async = true)]` for module functions and
  userdata instance methods returning `ValueTask` or `ValueTask<T>`.

## Async Userdata Methods

Async userdata methods reuse the module async bridge. The userdata `__index`
callback returns a PopLua async wrapper around the raw generated method
callback. The raw callback reads the userdata receiver and arguments, starts a
known `ValueTask`/`ValueTask<T>`, and either completes synchronously or returns
an internal operation token to the Lua wrapper.

The receiver is read before suspension. If the method suspends, the managed
async state machine holds the receiver reference until completion while
`Session` owns operation lifetime, cancellation, quotas, diagnostics, and
disposal. Async properties, fields, operators, constructors, and finalizers are
not generated as async Lua functions.

## Lua 5.4 or 5.5 Coroutine Constraints

The Lua 5.4 or 5.5 manual defines coroutine execution around separate Lua threads:
`lua_newthread` creates a thread with its own stack and shared global
environment, `lua_resume` starts or resumes it, and `lua_resume` returns
`LUA_YIELD` when the coroutine suspends. The yielded values remain on the
coroutine stack until the host removes them and resumes the coroutine.

Important C API constraints:

- Lua yields use C `longjmp`.
- Lua forbids yielding across ordinary C API frames. The exceptions are
  `lua_yieldk`, `lua_callk`, and `lua_pcallk`, which require continuation
  functions.
- `lua_yieldk` can fail if called from a non-yieldable thread or across a C-call
  boundary.
- `lua_status` reports whether a thread is normal, yielded, or failed.
- `lua_resume` returns errors instead of throwing them through the host.

These rules matter more for PopLua than for C hosts because PopLua callbacks
are managed methods entered from native Lua. PopLua already avoids calling
`lua_error` from generated managed callbacks to avoid native longjmp crossing
managed stack frames. The async bridge should preserve that rule.

Reference: Lua 5.4 or 5.5 manual sections
[4.5 Handling Yields in C](https://www.lua.org/manual/5.4/manual.html#4.5),
[`lua_newthread`](https://www.lua.org/manual/5.4/manual.html#lua_newthread),
[`lua_resume`](https://www.lua.org/manual/5.4/manual.html#lua_resume),
[`lua_status`](https://www.lua.org/manual/5.4/manual.html#lua_status), and
[`lua_yieldk`](https://www.lua.org/manual/5.4/manual.html#lua_yieldk).

## Candidate Architectures

### Option A: Direct C Callback Yield/Resume

Generated async callbacks would call `lua_yieldk` directly after starting a
`ValueTask`.

Benefits:

- Closest to the simple specification wording.
- Lua source sees `local value = api.fetch(...)`.
- Uses real Lua coroutine yield/resume.

Problems:

- Calling `lua_yieldk` from generated managed callbacks would longjmp out of a
  managed frame.
- Continuation callbacks would have to be unmanaged function pointers and would
  need to recover typed async state.
- This reintroduces the same managed/native hazard PopLua avoided for callback
  errors.
- It makes generated binding code tightly coupled to Lua continuation mechanics.

Conclusion: reject this form. Real Lua coroutine yield/resume is still desirable,
but direct managed-callback `lua_yieldk` is not the safe implementation.

### Option B: PopLua Scheduler Layer

PopLua would manage suspended executions and outstanding async operations above
Lua.

Benefits:

- Gives the runtime one place to own cancellation, quotas, diagnostics, and
  disposal.
- Keeps generated code small.
- Matches the existing `Session` ownership model.

Problems:

- If implemented without real Lua coroutines, Lua code cannot naturally write
  `local value = api.fetch(...)`; it would receive a placeholder value.
- It introduces execution-state complexity that does not exist today.

Conclusion: accept as part of the solution, but only when paired with real Lua
coroutine threads.

### Option C: Task/Userdata Model

Async methods would return a Lua-visible task object:

```lua
local task = api.fetch("/status")
local value = task:await()
```

Benefits:

- Avoids hiding coroutine mechanics in generated functions.
- Easier to implement incrementally.
- Could expose cancellation and polling explicitly.

Problems:

- It does not match the v2 specification's synchronous-looking Lua call shape.
- It leaks host async concepts into Lua scripts.
- `task:await()` still needs a yield strategy or it blocks.
- It creates a second user-facing async API that may be hard to remove later.

Conclusion: reject for the core bridge. It could become an advanced API later,
but it should not be the primary implementation.

### Option D: Lua Wrapper Yield + PopLua Scheduler

Generated async callbacks do not yield. Instead, each public async Lua function
is a Lua wrapper around a raw generated C callback:

```lua
function api.fetch(...)
    local token = __poplua_raw_fetch(...)
    if not __poplua_async_is_ready(token) then
        __poplua_yield(token)
    end

    if __poplua_async_is_success(token) then
        return __poplua_async_take_result(token)
    end

    error(__poplua_async_error(token), 0)
end
```

The raw generated callback starts the `ValueTask` and returns an internal async
operation token. If the operation completes synchronously, the token is already
ready. If it completes asynchronously, the Lua wrapper yields the coroutine with
the token. `Session` awaits the operation, marks the token complete, and
resumes the coroutine. The wrapper then returns the typed result or raises a Lua
error.

Benefits:

- Preserves the Lua-facing syntax from the v2 spec:

  ```lua
  local body = api.fetch("/status")
  ```

- Uses real Lua coroutine threads and `lua_resume`.
- Avoids direct `lua_yieldk` from generated managed callbacks.
- Allows Lua `pcall` to catch async completion failures because the wrapper
  raises the error in Lua after resume.
- Keeps runtime scheduling, cancellation, disposal, and diagnostics in
  `Session`.
- Remains AOT-friendly: generated code starts known `ValueTask` types and passes
  known typed result marshalers.

Problems:

- Requires runtime support for hidden Lua wrappers and an internal yield function.
- Requires `Session` to execute chunks and calls on coroutine threads instead
  of the main state stack.
- Requires careful lifetime management for coroutine threads and async tokens.

Conclusion: recommended architecture.

## Recommended Architecture

PopLua should implement Option D: a hybrid of real Lua coroutine execution and a
PopLua-owned scheduler.

The public behavior remains:

```lua
local body = api.fetch("/status")
```

The internal behavior is:

1. `Session.Run` creates a Lua thread with `lua_newthread`.
2. The chunk is loaded onto that thread and started with `lua_resume`.
3. Generated async functions are registered or returned as Lua wrappers around
   raw C callbacks.
4. The raw generated callback starts a `ValueTask` and returns a hidden
   `LuaAsyncOperation` token.
5. The Lua wrapper yields the token using an internal captured yield function.
6. `Session` observes `LUA_YIELD`, reads the token, awaits the operation, and
   resumes the coroutine.
7. On completion, the Lua wrapper returns the result or raises a Lua error.
8. `Session` completes the outer `ValueTask<Result>` when the coroutine
   returns or fails.

The internal yield function should be a captured Lua `coroutine.yield` function,
not a managed C callback that calls `lua_yieldk`. For untrusted sessions, PopLua
may load the coroutine library internally and keep only the captured yield
function in the registry/upvalues. It must not expose the `coroutine` table as a
global unless the sandbox policy already allows standard libraries.

## Runtime Changes

### Native Surface

Add internal P/Invoke bindings only for the Lua coroutine and registry operations
needed by the runtime:

- `lua_newthread`
- `lua_resume`
- `lua_status`
- `lua_xmove` if values must move between main and coroutine stacks
- `luaL_ref`
- `luaL_unref`
- `lua_rawgeti`
- `lua_pushvalue`
- `luaopen_coroutine` or another internal way to capture `coroutine.yield`

These remain internal to `PopLua.Interop.Native`.

### Execution State

Introduce an internal execution object, for example:

```text
LuaExecution
  ScriptContext Context
  Chunk? Chunk
  nint Thread
  int ThreadRegistryRef
  int StackBase
  LuaAsyncOperation? PendingOperation
  Stopwatch WallClock
  Stopwatch ActiveClock
  long Instructions
  bool IsDisposedOrCanceled
```

`Session` should allow at most one active execution at a time. Starting
another `Run`, `Run<T>`, or `Call` while an execution is running or suspended is
host misuse and should throw directly.

### Running Chunks

`Run(Chunk)` should become a real async method:

1. Create `ScriptContext`.
2. Emit `Diagnostics.Started`.
3. Create and registry-reference a coroutine thread.
4. Register the thread state in the session lookup used by callbacks.
5. Install quota hooks on the coroutine thread.
6. Load the chunk on the coroutine thread.
7. Resume until completion, failure, cancellation, or suspension.
8. On `LUA_YIELD`, read the yielded async token and await it without holding
   active Lua execution time open.
9. Resume with no user values; the token stores completion state.
10. Read final results, restore stacks, unref the thread, unregister the thread,
    and emit `Completed` or `Failed`.

`Call(...)` should use the same execution loop. It should push the global
function and arguments onto a new coroutine thread and call `lua_resume`.

### Async Operation Token

`LuaAsyncOperation` should be internal. It should hold:

- completion state;
- result or exception;
- cancellation state;
- a generated result pusher;
- optional operation name for diagnostics;
- exactly one await path for the underlying `ValueTask`.

The token exposed to Lua should be hidden userdata with an internal metatable.
Scripts should not be able to construct or inspect it without debug privileges.

Generated code should call typed helper methods such as:

```text
Marshaller.BeginAsync<T>(state, ValueTask<T> task, Func<nint, T, int> push, bool pauseTime)
Marshaller.BeginAsync(state, ValueTask task, bool pauseTime)
Marshaller.BeginFailedAsync(state, Exception error)
```

The helpers push a token and return `1`. They do not yield.

### Internal Lua Wrapper

`Registration` should gain an internal async registration path:

```text
AsyncModuleFunction(moduleName, luaName, rawCallback)
```

It should register a public Lua function equivalent to:

```lua
local raw = ...
local is_ready = ...
local yield = ...
local is_success = ...
local error_message = ...
local take_result = ...

return function(...)
    local token = raw(...)

    if not is_ready(token) then
        yield(token)
    end

    if is_success(token) then
        return take_result(token)
    end

    local message = error_message(token)
    release(token)
    error(message, 0)
end
```

`take_result` is a non-yielding C helper that invokes the operation's generated
result pusher, releases the token handle, and may return zero, one, or multiple
Lua values.

### Context

`Session.CurrentContext` should remain thread-static during `lua_resume`.
Before every resume, set it to the active execution context. Restore it when
`lua_resume` returns.

The `Sessions` lookup must map both the main state and active coroutine thread
states to the owning `Session`, because generated callbacks receive the
running thread state.

### Generator Impact

For module methods with `[Fn(Async = true)]`:

- accept `ValueTask` and `ValueTask<T>` as return types;
- map `ValueTask<T>` to Lua-facing return type `T`;
- generate a raw callback that starts the task and returns an async token;
- catch synchronous start failures and return a failed async token instead of
  using the current managed-error side channel;
- register the method through `Registration.AsyncModuleFunction`.

For module methods without `Async = true`:

- keep the existing synchronous code path.

For userdata methods with `[Fn(Async = true)]`:

- accept `ValueTask` and `ValueTask<T>` as return types;
- map `ValueTask<T>` to Lua-facing return type `T`;
- return wrapper closures from `__index` so method calls can yield through the
  same Lua wrapper used by module functions;
- read the userdata receiver before starting the task;
- catch synchronous start failures and return a failed async token.

For userdata methods without `Async = true`:

- keep the existing synchronous code path.

## Cancellation

`ScriptContext.Cancellation` should be the linked token for the active execution.

Recommended behavior:

- If cancellation is requested while Lua is actively running, the debug hook
  should stop execution at the next hook check with a terminal failed
  `Result`.
- If cancellation is requested while suspended, `Session` should stop waiting
  and complete the script as a terminal failed `Result`.
- Cancellation should not be catchable by Lua `pcall`, because host cancellation
  is a control-plane decision.
- If an async operation faults with `OperationCanceledException` and the context
  token is canceled, treat it as terminal cancellation.
- If an async operation faults with another exception, resume the coroutine and
  let the Lua wrapper raise a catchable Lua error.

The current spec says cancellation completes the script with a
`ScriptException`. That can remain true initially. A dedicated
`LuaCancellationException` can be considered later, but it is not required for
the bridge.

## Quotas And Metrics

Instruction quota:

- Count only Lua VM instructions while the coroutine is running.
- Debug hooks must be installed on coroutine threads, not only the main state.

Active-time quota:

- Treat sandbox active-time quota as active Lua execution time.
- Generated async functions default to pausing active-time accounting while
  actually awaiting host `ValueTask`s.
- `[Fn(Async = true, PauseTime = false)]` keeps active-time accounting
  running during suspended awaits for host work that should consume the active
  budget.
- C# work before the first await and after resumption always counts as active
  time.

Wall-time quota:

- Count total elapsed execution lifetime, including active Lua execution,
  synchronous host binding work, and suspended async waits.
- Wall-time is the absolute lifetime limit and is never paused by `PauseTime`.

Metrics:

- Existing `Metrics.Duration` should remain total wall-clock duration unless
  the public diagnostics API is revised.
- Internally track active Lua time and suspended time so a future diagnostics
  extension can expose them without redesign.
- Update `ScriptContext.State` with instructions, elapsed time, peak memory, and
  current call-depth data.

Memory and call-depth:

- Coroutine stacks and async operation userdata are allocated through the
  PopLua-owned Lua allocator and count toward the memory quota.
- Call-depth tracking is maintained across suspension and resume because the
  execution coroutine remains active while suspended.

## Diagnostics

The existing diagnostics interface can support the first implementation:

- `Started` when a run/call begins.
- `Completed` when the coroutine returns successfully.
- `Failed` when load, resume, cancellation, quota, sandbox, or async failure
  terminates execution.
- `QuotaBlocked` and `SandboxBlocked` keep their current meanings.

Suspended/resumed events are useful, but adding them to `IDiagnostics` is a
public breaking change. Prefer one of these approaches:

1. Phase 1: keep suspension/resume internal and expose only final metrics.
2. Before v2 API freeze: add an optional secondary interface, for example
   `ILuaAsyncDiagnostics`, with `Suspended` and `Resumed` methods.

Do not block the bridge on public suspend/resume diagnostics.

## Error Semantics

Top-level Lua errors:

- `lua_resume` error statuses become failed `Result` values, matching current
  `lua_pcallk` behavior.

Synchronous generated callback errors:

- Existing synchronous callbacks continue using the managed-error side channel.
- Lua `pcall` cannot reliably catch those failures. For `1.0`, this is a
  documented runtime limitation rather than an open implementation gap: PopLua
  avoids native `lua_error`/longjmp across managed callback frames and reports
  the failure through the outer `Result`.

Async start errors:

- Generated async callbacks should catch exceptions raised while reading
  arguments, resolving services, checking sandbox capabilities, or starting the
  `ValueTask`.
- Those failures should become completed failed async tokens.
- The Lua wrapper raises them with `error(...)`, so Lua `pcall` can catch them.

Async completion errors:

- Managed task faults should be stored on the token.
- After resume, the Lua wrapper raises a Lua error.
- `pcall` around the async call should return `false, message`.
- If not caught, `Session.Run` returns a failed `Result`.

Cancellation:

- Host cancellation should be terminal and not catchable by Lua.
- It should surface as a failed `Result`, initially using `ScriptException`
  as the spec already states.

Nested Lua -> C# -> Lua:

- A managed async operation must not call back into the same `Session` while
  that session is suspended.
- `Session` should enforce a single active execution and throw direct host
  misuse exceptions for reentrant `Run`/`Call`.
- Nested work can use a separate `Session`.

## Lifetime

Coroutine lifetime:

- Every execution coroutine must be registry-referenced while active or
  suspended.
- The reference must be released on success, failure, cancellation, and disposal.
- Thread states must be cleared from the session lookup before the coroutine can
  be collected.

Async operation lifetime:

- Operation tokens are Lua userdata and should hold a managed handle to the
  internal operation.
- The token's `__gc` path should release the handle if the operation was
  abandoned.
- The wrapper and scheduler release token handles after success, Lua-error
  faults, managed-error faults, and terminal cancellation. The token's `__gc`
  path remains a fallback for abandoned userdata.

`ValueTask` lifetime:

- Await each `ValueTask` exactly once.
- Completed `ValueTask` results may be read synchronously.
- Incomplete `ValueTask`s may be converted to `Task` internally if necessary to
  store them safely. This allocation is acceptable for true async suspension.

Session disposal:

- `DisposeAsync` should cancel the active execution if one exists.
- The Lua state must not be closed while the PopLua scheduler can still attempt
  to resume it.
- Disposal awaits active execution cleanup. Suspension waits use the linked
  execution token, so disposal is deterministic even if the host operation does
  not complete cooperatively.

## AOT Review

The recommended design remains AOT-compatible:

- No runtime reflection.
- No dynamic method generation.
- No expression compilation.
- Generated code references concrete `ValueTask` and result types.
- Internal generic helpers are closed by generated call sites.
- Result pushers are generated static methods or cached delegates.
- Native interop additions remain internal P/Invoke bindings to Lua 5.4 or 5.5.

AOT risks to watch:

- Avoid relying on reflection to discover `ValueTask<T>` result types.
- Avoid dynamically constructing generic marshalers.
- Keep async operation result pushers generated and statically referenced.
- Avoid exposing new public interop surfaces.

## Implemented Milestones

### Phase 1: Coroutine Execution Foundation

- Add internal Lua coroutine native bindings.
- Introduce internal `LuaExecution`.
- Run `Run` and `Call` through `lua_newthread` + `lua_resume`.
- Preserve current synchronous behavior and tests.
- Register coroutine thread states in the session lookup.
- Install quota hooks on execution threads.
- Add tests for successful run, script error, `pcall`, quota, `Call`, bytecode,
  and session disposal under coroutine execution.

### Phase 2: Internal Async Operation Runtime

- Add hidden `LuaAsyncOperation` and token userdata.
- Add internal token helpers: ready, success, error, take-result.
- Add internal captured `coroutine.yield` wrapper support.
- Add scheduler loop in `Session` for `LUA_YIELD`.
- Add cancellation handling while suspended.
- Add tests for suspend/resume, completion, failure, cancellation, disposal, and
  reentrant session use.

### Phase 3: Generator Support For Async Module Functions

- Teach `IsSupportedReturn` that `ValueTask` and `ValueTask<T>` are valid only
  when `[Fn(Async = true)]` is set.
- Generate raw async callbacks and typed result pushers.
- Register async functions through `AsyncModuleFunction`.
- Add diagnostics for invalid async return types.
- Add generator tests for `ValueTask`, `ValueTask<T>`, invalid return types,
  generated async registration, typed result pushers, userdata async wrappers,
  and no runtime reflection.

### Phase 4: Runtime Semantics Tests

- `local value = api.fetch(...)` returns the awaited value.
- Completed synchronously does not suspend externally.
- Lua `pcall(api.fetch, ...)` catches async start and completion failures.
- Uncaught async failure returns failed `Result`.
- Cancellation while suspended returns failed `Result`.
- Instruction quota still works after resume.
- Active-time quota excludes suspended await time by default, and includes it
  when the async generated function sets `PauseTime = false`.
- Multiple async calls in sequence work.
- Multiple async calls inside Lua control flow work.
- Concurrent use of the same session while suspended throws host misuse.
- Disposing a session with an outstanding operation cancels and cleans up.

### Phase 5: Examples And Docs

- Replace blocking examples in `examples/Async.cs` and `examples/EventHandler.cs`
  with `[Fn(Async = true)]`.
- Update getting-started and technical reference docs.
- Keep API manifest metadata aligned with `async: true`.

## Specification Status

The v2 spec reflects these bridge semantics:

- Async bridge uses real Lua coroutine threads owned by `Session`.
- Generated async callbacks must not directly call `lua_yieldk`.
- Async module functions are exposed through internal Lua wrappers that yield
  with a hidden operation token.
- The bridge supports generated module functions and userdata instance methods.
- Host cancellation is terminal and not catchable by Lua `pcall`.
- Managed async task faults are Lua errors and are catchable by Lua `pcall`.
- Lua active-time quota counts active execution by default, with per-function
  suspended-time policy through `[Fn(PauseTime = ...)]`. Wall-time quota
  counts the full elapsed execution lifetime.
- `Session` allows only one active or suspended execution at a time.

## Recommended Decision

Implement Option D: Lua wrapper yield plus PopLua scheduler.

It best fits PopLua because it keeps the public Lua experience promised by the
spec, uses Lua coroutines for real suspension, preserves AOT/source-generation
constraints, avoids longjmp across managed callback frames, and centralizes
execution lifecycle concerns in `Session`.

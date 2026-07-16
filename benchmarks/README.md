# PopLua Benchmarks

Repeatable runtime benchmarks for callback and userdata overhead live in
`PopLua.Benchmarks`.

Run:

```bash
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*UserdataRuntimeBenchmarks*' --job short
```

Use `--job dry` only as a harness smoke check:

```bash
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*StartupBenchmarks*' --job dry
```

Dry jobs use a single cold launch/iteration shape and include enough
BenchmarkDotNet harness/process noise that millisecond-scale startup numbers
from dry output are not comparable with the short-run baseline tables below.
Treat dry output as "the benchmark builds and executes," not as a performance
claim or regression signal.

The suite isolates:

- pure Lua loop baseline;
- generated module function callback overhead;
- userdata construction, including managed handle allocation;
- userdata construction separated from property reads;
- userdata parameter reads and type checks;
- userdata return/wrapper creation without managed object construction;
- userdata method dispatch;
- userdata property dispatch through `__index`;
- userdata operator dispatch through metamethods;
- a userdata-heavy loop that combines allocation, operators, properties, and methods.
- managed-only controls for object allocation, `GCHandle.Alloc`/`Free`, and
  direct C# method calls.
- KeraLua low-level controls for pure Lua and direct C# callback loops.
- completed async module call overhead versus synchronous module calls.
- completed async userdata method overhead versus synchronous userdata methods.
- suspended async userdata method overhead versus synchronous userdata methods.
- descriptor conversion, descriptor-list conversion, mixed `Value[]`
  varargs, and computed module property access with `[Context]`.
- runtime/session startup cost versus raw KeraLua state creation.

These benchmarks are intentionally observational. They should guide future
optimization work, not encode speculative rewrites into the runtime.
KeraLua is referenced only by the benchmark project; it is not a PopLua package
dependency.

## Baseline Findings

The tables below are short-run baselines. They are more useful than dry smoke
checks for relative cost comparisons, but they are still visibility numbers, not
stable release performance claims.

Short-run baseline captured with:

```bash
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*UserdataRuntimeBenchmarks*' --job short --warmupCount 1 --iterationCount 3
```

Managed-only controls were captured with:

```bash
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*ManagedRuntimeCostBenchmarks*' --job short --warmupCount 1 --iterationCount 3
```

Comparison and async controls were captured with:

```bash
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*KeraLuaComparisonBenchmarks*' --job short --warmupCount 1 --iterationCount 3
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*AsyncBridgeBenchmarks*' --job short --warmupCount 1 --iterationCount 3
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*AsyncBridgeSuspensionBenchmarks*' --job short --warmupCount 1 --iterationCount 3
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*AsyncBridgeFaultBenchmarks*' --job short --warmupCount 1 --iterationCount 3
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*AsyncUserdataBenchmarks*' --job short --warmupCount 1 --iterationCount 3
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*AsyncUserdataSuspensionBenchmarks*' --job short --warmupCount 1 --iterationCount 3
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*DescriptorAndContextBenchmarks*' --job short --warmupCount 1 --iterationCount 3
dotnet run -c Release --project benchmarks/PopLua.Benchmarks -- --filter '*StartupBenchmarks*' --job short --warmupCount 1 --iterationCount 3
```

Environment:

- BenchmarkDotNet `0.15.2`
- .NET SDK `10.0.108`
- .NET runtime `10.0.8`
- Intel Xeon E5-2650 v4, Linux x64

Current results for `Iterations = 100_000`. These are short-run visibility
numbers, not stable release numbers.

| Benchmark | Mean | Ratio vs pure Lua | Managed allocation |
|---|---:|---:|---:|
| Pure Lua loop | 0.660 ms | 1.0x | 832 B |
| Module function callback loop | 17.177 ms | 26.0x | 832 B |
| Userdata construction only loop | 110.769 ms | 167.8x | 3.2 MB |
| Userdata construction + property loop | 141.501 ms | 214.4x | 3.2 MB |
| Userdata method call loop | 28.071 ms | 42.5x | 864 B |
| Userdata property access loop | 24.970 ms | 37.8x | 864 B |
| Userdata parameter read loop | 19.161 ms | 29.0x | 864 B |
| Userdata echo return loop | 90.701 ms | 137.4x | 864 B |
| Userdata operator invocation loop | 103.611 ms | 157.0x | 3.2 MB |
| Userdata-heavy loop | 366.880 ms | 555.9x | 9.6 MB |

Previous short-run userdata results before UTF-8 key dispatch:

| Benchmark | Previous mean | Previous allocation | Current mean | Current allocation |
|---|---:|---:|---:|---:|
| Userdata method call loop | 31.985 ms | 4.0 MB | 28.071 ms | 864 B |
| Userdata property access loop | 28.235 ms | 2.4 MB | 24.970 ms | 864 B |
| Userdata-heavy loop | 349.991 ms | 16.0 MB | 366.880 ms | 9.6 MB |

Managed-only controls:

| Benchmark | Mean | Managed allocation |
|---|---:|---:|
| Direct C# method call loop | 0.278 ms | 0 B |
| Managed object allocation loop | 0.769 ms | 3.2 MB |
| `GCHandle.Alloc`/`Free` loop | 5.667 ms | 0 B |

KeraLua low-level controls, also at `Iterations = 100_000`:

| Benchmark | Mean | Ratio vs KeraLua pure Lua | Managed allocation |
|---|---:|---:|---:|
| KeraLua pure Lua loop | 0.647 ms | 1.0x | 0 B |
| KeraLua C# callback loop | 9.258 ms | 14.3x | 0 B |

Completed async bridge controls, at `Iterations = 10_000`:

| Benchmark | Mean | Ratio vs sync module loop | Managed allocation |
|---|---:|---:|---:|
| Sync module loop | 1.627 ms | 1.0x | 832 B |
| Completed async module loop | 2.712 ms | 1.7x | 832 B |

Before the completed-async fast path, the same short-run completed async module
loop measured about `12.149 ms` per 10k calls and allocated about `3.6 MB`.
The fast path removes operation-token allocation for already-completed successful
`ValueTask` and `ValueTask<T>` calls while preserving the token path for faults,
cancellation, and real suspension.

Forced suspension controls, at `Iterations = 1_000`:

| Benchmark | Mean | Ratio vs sync module loop | Managed allocation |
|---|---:|---:|---:|
| Sync module loop | 164.8 us | 1.0x | 832 B |
| Suspended async module loop | 4.579 ms | 27.8x | 530.9 KB |

Completed async fault path control:

| Benchmark | Mean | Managed allocation |
|---|---:|---:|
| Completed async fault caught by `pcall` | 12.47 us | 1.81 KB |

Async userdata method controls:

| Benchmark | Mean | Ratio vs sync userdata method loop | Managed allocation |
|---|---:|---:|---:|
| Sync userdata method loop | 3.256 ms | 1.0x | 904 B |
| Completed async userdata method loop | 27.294 ms | 8.4x | 320.9 KB |

Before caching the async wrapper factory per Lua state, the same completed
async userdata loop measured about `183.971 ms` per 10k calls and allocated the
same managed memory. The cache avoids loading the generated Lua wrapper on every
userdata `__index` lookup; it does not remove the remaining wrapper, coroutine,
or closure overhead.

Suspended async userdata method controls, at `Iterations = 1_000`:

| Benchmark | Mean | Ratio vs sync userdata method loop | Managed allocation |
|---|---:|---:|---:|
| Sync userdata method loop | 311.8 us | 1.0x | 904 B |
| Suspended async userdata method loop | 14.397 ms | 46.2x | 828.0 KB |

Startup controls:

| Benchmark | Mean | Managed allocation |
|---|---:|---:|
| PopLua runtime + trusted session | 55.32 us | 496 B |
| KeraLua state with libraries | 52.29 us | 40 B |

Interpretation:

- Userdata-heavy loops remain expensive today. The dominant cost is repeated
  Lua/C# boundary crossing plus Lua userdata allocation, metatable assignment,
  handle allocation/free, and dispatch. Plain managed object allocation is not
  enough to explain the Lua userdata construction cost.
- Generated module callback overhead is measurable even without userdata:
  crossing Lua -> C# 100k times is roughly 26x the pure Lua loop baseline in the
  short run.
- KeraLua's direct C# callback loop is a useful lower-level comparison at roughly
  14x its pure Lua baseline. PopLua's generated module loop is slower, as
  expected, because it adds generated marshaling, session state, diagnostics/error
  side-channel handling, and sandbox-compatible runtime structure.
- `GCHandle.Alloc`/`Free` is material at about 5.7 ms per 100k in the managed
  control, but full userdata construction is much higher, so Lua userdata
  allocation/metatable work and callback dispatch also matter.
- Completed successful async module calls now bypass operation-token allocation
  when the returned `ValueTask` is already complete. They remain slower than sync
  module calls because they still run through the generated async wrapper shape,
  but the allocation profile now matches the sync loop in the short run.
- Genuinely suspended async module calls still pay the scheduler, coroutine, and
  operation-token cost. That path is intentionally preserved for cancellation,
  completion races, and deterministic disposal behavior.
- Completed async userdata methods benefit from the same operation-token fast
  path as async module functions, but remain much slower than synchronous
  userdata methods because each Lua method lookup still returns an async wrapper
  closure.
- Suspended async userdata methods pay both userdata method dispatch and the
  async scheduler/token path. The short-run result should be treated as a
  visibility baseline, not a release claim.
- Faulted completed async calls still use the token/error path so Lua `pcall`
  catches task faults consistently with suspended async failures.
- Runtime/session startup is close to raw KeraLua state creation in the short
  run. Startup is not currently the dominant performance concern.
- Userdata parameter type checking is not dominant by itself. After avoiding
  managed string allocation for userdata `__index` keys, the method and property
  dispatch cases allocate almost nothing managed.
- Userdata method and property dispatch are close to each other. Avoiding
  per-lookup managed string allocation from `__index` removed most managed
  allocation from those loops and slightly improved time. A cached-metatable
  method lookup was measured and rejected because it reduced allocation but made
  method/property dispatch materially slower in the short run.
- Operator dispatch is close to construction cost because the tested `+`
  operator returns a new userdata every iteration.
- Future optimization work should first target userdata allocation/wrapper
  pressure, method lookup/closure creation, property key dispatch, and repeated
  Lua/C# transitions, while preserving AOT compatibility and generated bindings.

## Ranked Performance Backlog

1. Reduce userdata allocation/wrapper pressure.
   Userdata construction, echo returns, operators that create objects, and the
   userdata-heavy loop dominate the current baseline. This likely means future
   work should inspect Lua userdata allocation, metatable assignment, managed
   handle lifetime, and wrapper reuse options before touching smaller dispatch
   details.

2. Reduce repeated Lua/C# transition counts in userdata-heavy code paths.
   Module callbacks are already about 23x the pure Lua loop in the short run.
   Userdata-heavy loops multiply that cost through construction, property access,
   operators, and methods. Future API-shape or generator work should prefer
   coarser generated calls when host APIs naturally support them.

3. Reduce genuinely suspended async overhead.
   Completed successful async calls now have a direct fast path. Suspended calls
   remain much more expensive because they allocate operation state and involve
   coroutine scheduler transitions. Future work should only target this if real
   workloads are dominated by fine-grained suspended async calls.

4. Improve userdata method/property dispatch.
   Method and property loops sit near 42x-48x the pure Lua baseline. The likely
   costs are `__index`, Lua string key lookup, generated callback dispatch, and
   closure/property result handling. Any optimization here should be measured
   against module callback overhead so it does not chase noise.

5. Investigate operator dispatch separately from allocation.
   The current operator benchmark returns a fresh userdata per iteration, so it
   mostly measures operator dispatch plus construction. Add an operator benchmark
   that returns a primitive or mutates a reusable value only if that maps to a
   real PopLua API pattern.

6. Keep `GCHandle.Alloc`/`Free` visible but secondary.
   Managed controls show handle allocation is material, but it is not large
   enough alone to explain full userdata construction. Handle pooling or alternate
   ownership schemes need careful safety review and should not be the first
   speculative change.

No optimization should compromise AOT compatibility, source-generator ownership,
internal interop encapsulation, deterministic userdata finalization behavior, or
sandbox safety.

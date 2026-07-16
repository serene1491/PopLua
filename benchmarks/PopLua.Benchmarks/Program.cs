using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;
using System.Runtime.InteropServices;

BenchmarkSwitcher.FromAssembly(typeof(UserdataRuntimeBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class UserdataRuntimeBenchmarks
{
    private Engine _runtime = null!;
    private Session _session = null!;
    private Bytecode _pureLuaLoop = null!;
    private Bytecode _moduleFunctionLoop = null!;
    private Bytecode _userdataConstructionOnlyLoop = null!;
    private Bytecode _userdataConstructionLoop = null!;
    private Bytecode _userdataMethodLoop = null!;
    private Bytecode _userdataPropertyLoop = null!;
    private Bytecode _userdataParameterLoop = null!;
    private Bytecode _userdataEchoLoop = null!;
    private Bytecode _userdataOperatorLoop = null!;
    private Bytecode _userdataHeavyLoop = null!;

    [Params(100_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _runtime = Engine.Create(b => b.Modules<PerfModule, PerfVecModule, PerfAsyncModule, PerfAsyncUserModule>());
        _session = _runtime.Session(Sandbox.Trusted);

        _pureLuaLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + i
            end
            return sum
            """);

        _moduleFunctionLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf.add(i, 1)
            end
            return sum
            """);

        _userdataConstructionOnlyLoop = Compile($$"""
            for i = 1, {{Iterations}} do
                local value = perf_vec.new(i, i)
            end
            return {{Iterations}}
            """);

        _userdataConstructionLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                local value = perf_vec.new(i, i)
                sum = sum + value.x
            end
            return sum
            """);

        _userdataMethodLoop = Compile($$"""
            local value = perf_vec.new(3, 4)
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + value:length()
            end
            return sum
            """);

        _userdataPropertyLoop = Compile($$"""
            local value = perf_vec.new(3, 4)
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + value.x
            end
            return sum
            """);

        _userdataParameterLoop = Compile($$"""
            local value = perf_vec.new(3, 4)
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf_vec.consume(value)
            end
            return sum
            """);

        _userdataEchoLoop = Compile($$"""
            local value = perf_vec.new(3, 4)
            for i = 1, {{Iterations}} do
                local echoed = perf_vec.echo(value)
            end
            return {{Iterations}}
            """);

        _userdataOperatorLoop = Compile($$"""
            local step = perf_vec.new(1, 1)
            local value = perf_vec.new(0, 0)
            for i = 1, {{Iterations}} do
                value = value + step
            end
            return value.x
            """);

        _userdataHeavyLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                local value = perf_vec.new(i, i) + perf_vec.new(1, 1)
                sum = sum + value.x + value:length()
            end
            return sum
            """);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _session.DisposeAsync();
        await _runtime.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<double> PureLuaLoop()
        => await Run(_pureLuaLoop);

    [Benchmark]
    public async Task<double> ModuleFunctionCallLoop()
        => await Run(_moduleFunctionLoop);

    [Benchmark]
    public async Task<double> UserdataConstructionOnlyLoop()
        => await Run(_userdataConstructionOnlyLoop);

    [Benchmark]
    public async Task<double> UserdataConstructionLoop()
        => await Run(_userdataConstructionLoop);

    [Benchmark]
    public async Task<double> UserdataMethodCallLoop()
        => await Run(_userdataMethodLoop);

    [Benchmark]
    public async Task<double> UserdataPropertyAccessLoop()
        => await Run(_userdataPropertyLoop);

    [Benchmark]
    public async Task<double> UserdataParameterReadLoop()
        => await Run(_userdataParameterLoop);

    [Benchmark]
    public async Task<double> UserdataEchoReturnLoop()
        => await Run(_userdataEchoLoop);

    [Benchmark]
    public async Task<double> UserdataOperatorInvocationLoop()
        => await Run(_userdataOperatorLoop);

    [Benchmark]
    public async Task<double> UserdataHeavyLoop()
        => await Run(_userdataHeavyLoop);

    private Bytecode Compile(string code)
        => _session.Compile(code);

    private async Task<double> Run(Bytecode bytecode)
    {
        var result = await _session.Run<Value>(bytecode);
        return result.Unwrap().Number();
    }
}

[Module("perf")]
public partial class PerfModule
{
    [Fn("add")]
    public static long Add(long left, long right) => left + right;
}

[Module("perf_vec")]
public partial class PerfVecModule
{
    [Fn("new")]
    public static PerfVec New(double x, double y) => new(x, y);

    [Fn("consume")]
    public static long Consume(PerfVec value)
        => value.X > 0 ? 1 : 0;

    [Fn("echo")]
    public static PerfVec Echo(PerfVec value) => value;
}

[Userdata("perf_vec")]
public partial class PerfVec(double x, double y)
{
    [Prop("x", ReadOnly = true)]
    public double X { get; } = x;

    [Prop("y", ReadOnly = true)]
    public double Y { get; } = y;

    [Fn("length")]
    public double Length() => Math.Sqrt(X * X + Y * Y);

    public static PerfVec operator +(PerfVec left, PerfVec right)
        => new(left.X + right.X, left.Y + right.Y);
}

[MemoryDiagnoser]
public class ManagedRuntimeCostBenchmarks
{
    private readonly PerfVec _value = new(3, 4);

    [Params(100_000)]
    public int Iterations { get; set; }

    [Benchmark]
    public double ManagedObjectAllocationLoop()
    {
        double sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var value = new PerfVec(i, i);
            sum += value.X;
        }

        return sum;
    }

    [Benchmark]
    public int GCHandleAllocFreeLoop()
    {
        var count = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var handle = GCHandle.Alloc(_value);
            if (handle.Target is not null)
                count++;

            handle.Free();
        }

        return count;
    }

    [Benchmark]
    public int DirectMethodCallLoop()
    {
        var count = 0;
        for (var i = 0; i < Iterations; i++)
            count += _value.Length() > 0 ? 1 : 0;

        return count;
    }
}

[MemoryDiagnoser]
public class KeraLuaComparisonBenchmarks
{
    private static readonly KeraLua.LuaFunction AddCallback = static state =>
    {
        var lua = KeraLua.Lua.FromIntPtr(state) ?? throw new InvalidOperationException("Lua state was not found.");
        lua.PushInteger(lua.ToInteger(1) + lua.ToInteger(2));
        return 1;
    };

    private KeraLua.Lua _lua = null!;
    private int _pureLuaLoop;
    private int _callbackLoop;

    [Params(100_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _lua = new KeraLua.Lua(openLibs: true);
        _lua.Register("add", AddCallback);

        _pureLuaLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + i
            end
            return sum
            """);

        _callbackLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + add(i, 1)
            end
            return sum
            """);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_pureLuaLoop != 0)
            _lua.Unref(KeraLua.LuaRegistry.Index, _pureLuaLoop);
        if (_callbackLoop != 0)
            _lua.Unref(KeraLua.LuaRegistry.Index, _callbackLoop);

        _lua.Dispose();
    }

    [Benchmark(Baseline = true)]
    public double PureLuaLoop()
        => Run(_pureLuaLoop);

    [Benchmark]
    public double CSharpCallbackLoop()
        => Run(_callbackLoop);

    private int Compile(string code)
    {
        var status = _lua.LoadString(code);
        if (status != KeraLua.LuaStatus.OK)
            throw new InvalidOperationException(ReadError());

        return _lua.Ref(KeraLua.LuaRegistry.Index);
    }

    private double Run(int reference)
    {
        _lua.RawGetInteger(KeraLua.LuaRegistry.Index, reference);
        var status = _lua.PCall(arguments: 0, results: 1, errorFunctionIndex: 0);
        if (status != KeraLua.LuaStatus.OK)
            throw new InvalidOperationException(ReadError());

        var result = _lua.ToNumber(-1);
        _lua.Pop(1);
        return result;
    }

    private string ReadError()
    {
        var message = _lua.ToString(-1) ?? "KeraLua execution failed.";
        _lua.Pop(1);
        return message;
    }
}

[MemoryDiagnoser]
public class AsyncBridgeBenchmarks
{
    private Engine _runtime = null!;
    private Session _session = null!;
    private Bytecode _syncLoop = null!;
    private Bytecode _completedAsyncLoop = null!;

    [Params(10_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _runtime = Engine.Create(b => b.Modules<PerfModule, PerfVecModule, PerfAsyncModule, PerfAsyncUserModule>());
        _session = _runtime.Session(Sandbox.Trusted);

        _syncLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf_async.add_sync(i, 1)
            end
            return sum
            """);

        _completedAsyncLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf_async.add_async(i, 1)
            end
            return sum
            """);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _session.DisposeAsync();
        await _runtime.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<double> SyncModuleLoop()
        => await Run(_syncLoop);

    [Benchmark]
    public async Task<double> CompletedAsyncModuleLoop()
        => await Run(_completedAsyncLoop);

    private Bytecode Compile(string code)
        => _session.Compile(code);

    private async Task<double> Run(Bytecode bytecode)
    {
        var result = await _session.Run<Value>(bytecode);
        return result.Unwrap().Number();
    }
}

[MemoryDiagnoser]
public class DescriptorAndContextBenchmarks
{
    private Engine _runtime = null!;
    private Session _session = null!;
    private Bytecode _simpleDescriptorLoop = null!;
    private Bytecode _nestedDescriptorListLoop = null!;
    private Bytecode _contextFunctionLoop = null!;
    private Bytecode _contextPropertyLoop = null!;
    private Bytecode _mixedVarargsLoop = null!;

    [Params(10_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _runtime = Engine.Create(b => b.Modules<PerfDescriptorModule, PerfContextModule>());
        _session = _runtime.Session(Sandbox.Trusted, services: Services.Create().Add(new PerfRequest("Pop")));

        _simpleDescriptorLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf_desc.simple({ label = "item", count = i })
            end
            return sum
            """);

        _nestedDescriptorListLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf_desc.nested({
                    options = {
                        { label = "A", value = "a" },
                        { label = "B", value = "b" }
                    }
                })
            end
            return sum
            """);

        _contextFunctionLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + #perf_ctx.author_name()
            end
            return sum
            """);

        _contextPropertyLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + #perf_ctx.author.name
            end
            return sum
            """);

        _mixedVarargsLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf_desc.kinds(nil, true, i, "x", {}, function() end)
            end
            return sum
            """);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _session.DisposeAsync();
        await _runtime.DisposeAsync();
    }

    [Benchmark]
    public async Task<double> SimpleDescriptorLoop()
        => await Run(_simpleDescriptorLoop);

    [Benchmark]
    public async Task<double> NestedDescriptorListLoop()
        => await Run(_nestedDescriptorListLoop);

    [Benchmark]
    public async Task<double> ContextFunctionLoop()
        => await Run(_contextFunctionLoop);

    [Benchmark]
    public async Task<double> ContextComputedPropertyLoop()
        => await Run(_contextPropertyLoop);

    [Benchmark]
    public async Task<double> MixedValueVarargsLoop()
        => await Run(_mixedVarargsLoop);

    private Bytecode Compile(string code)
        => _session.Compile(code);

    private async Task<double> Run(Bytecode bytecode)
    {
        var result = await _session.Run<Value>(bytecode);
        return result.Unwrap().Number();
    }
}

[MemoryDiagnoser]
public class AsyncBridgeSuspensionBenchmarks
{
    private Engine _runtime = null!;
    private Session _session = null!;
    private Bytecode _syncLoop = null!;
    private Bytecode _suspendedAsyncLoop = null!;

    [Params(1_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _runtime = Engine.Create(b => b.Modules<PerfModule, PerfVecModule, PerfAsyncModule, PerfAsyncUserModule>());
        _session = _runtime.Session(Sandbox.Trusted);

        _syncLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf_async.add_sync(i, 1)
            end
            return sum
            """);

        _suspendedAsyncLoop = Compile($$"""
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + perf_async.add_suspended(i, 1)
            end
            return sum
            """);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _session.DisposeAsync();
        await _runtime.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<double> SyncModuleLoop()
        => await Run(_syncLoop);

    [Benchmark]
    public async Task<double> SuspendedAsyncModuleLoop()
        => await Run(_suspendedAsyncLoop);

    private Bytecode Compile(string code)
        => _session.Compile(code);

    private async Task<double> Run(Bytecode bytecode)
    {
        var result = await _session.Run<Value>(bytecode);
        return result.Unwrap().Number();
    }
}

[MemoryDiagnoser]
public class AsyncBridgeFaultBenchmarks
{
    private Engine _runtime = null!;
    private Session _session = null!;
    private Bytecode _faultCaughtByPCall = null!;

    [GlobalSetup]
    public void Setup()
    {
        _runtime = Engine.Create(b => b.Modules<PerfModule, PerfVecModule, PerfAsyncModule, PerfAsyncUserModule>());
        _session = _runtime.Session(Sandbox.Trusted);
        _faultCaughtByPCall = _session.Compile("""
            local ok, err = pcall(function()
                return perf_async.fault_completed()
            end)
            return ok == false and string.find(err, 'benchmark fault') ~= nil
            """);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _session.DisposeAsync();
        await _runtime.DisposeAsync();
    }

    [Benchmark]
    public async Task<bool> CompletedAsyncFaultCaughtByPCall()
    {
        var result = await _session.Run<bool>(_faultCaughtByPCall);
        return result.Unwrap();
    }
}

[MemoryDiagnoser]
public class AsyncUserdataBenchmarks
{
    private Engine _runtime = null!;
    private Session _session = null!;
    private Bytecode _syncLoop = null!;
    private Bytecode _completedAsyncLoop = null!;

    [Params(10_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _runtime = Engine.Create(b => b.Modules<PerfModule, PerfVecModule, PerfAsyncModule, PerfAsyncUserModule>());
        _session = _runtime.Session(Sandbox.Trusted);

        _syncLoop = Compile($$"""
            local value = perf_async_user.new()
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + value:add_sync(i, 1)
            end
            return sum
            """);

        _completedAsyncLoop = Compile($$"""
            local value = perf_async_user.new()
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + value:add_async(i, 1)
            end
            return sum
            """);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _session.DisposeAsync();
        await _runtime.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<double> SyncUserdataMethodLoop()
        => await Run(_syncLoop);

    [Benchmark]
    public async Task<double> CompletedAsyncUserdataMethodLoop()
        => await Run(_completedAsyncLoop);

    private Bytecode Compile(string code)
        => _session.Compile(code);

    private async Task<double> Run(Bytecode bytecode)
    {
        var result = await _session.Run<Value>(bytecode);
        return result.Unwrap().Number();
    }
}

[MemoryDiagnoser]
public class AsyncUserdataSuspensionBenchmarks
{
    private Engine _runtime = null!;
    private Session _session = null!;
    private Bytecode _syncLoop = null!;
    private Bytecode _suspendedAsyncLoop = null!;

    [Params(1_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _runtime = Engine.Create(b => b.Modules<PerfModule, PerfVecModule, PerfAsyncModule, PerfAsyncUserModule>());
        _session = _runtime.Session(Sandbox.Trusted);

        _syncLoop = Compile($$"""
            local value = perf_async_user.new()
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + value:add_sync(i, 1)
            end
            return sum
            """);

        _suspendedAsyncLoop = Compile($$"""
            local value = perf_async_user.new()
            local sum = 0
            for i = 1, {{Iterations}} do
                sum = sum + value:add_suspended(i, 1)
            end
            return sum
            """);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _session.DisposeAsync();
        await _runtime.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task<double> SyncUserdataMethodLoop()
        => await Run(_syncLoop);

    [Benchmark]
    public async Task<double> SuspendedAsyncUserdataMethodLoop()
        => await Run(_suspendedAsyncLoop);

    private Bytecode Compile(string code)
        => _session.Compile(code);

    private async Task<double> Run(Bytecode bytecode)
    {
        var result = await _session.Run<Value>(bytecode);
        return result.Unwrap().Number();
    }
}

[Module("perf_desc")]
public partial class PerfDescriptorModule
{
    [Fn("simple")]
    public static long Simple(PerfSimpleDescriptor descriptor)
        => descriptor.Count;

    [Fn("nested")]
    public static long Nested(PerfNestedDescriptor descriptor)
        => descriptor.Options.Count;

    [Fn("kinds")]
    public static long Kinds(Value[] values)
        => values.Length;
}

public sealed class PerfSimpleDescriptor
{
    public string? Label { get; init; }
    public long Count { get; init; }
}

public sealed class PerfNestedDescriptor
{
    public IReadOnlyList<PerfOptionDescriptor> Options { get; init; } = [];
}

public sealed class PerfOptionDescriptor
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}

[Module("perf_ctx")]
public partial class PerfContextModule
{
    [Fn("author_name")]
    public static string AuthorName([Context] ScriptContext context)
        => ((PerfRequest)context.Services.GetService(typeof(PerfRequest))!).Author;

    [Prop("author")]
    public static PerfAuthor Author([Context] ScriptContext context)
        => new(((PerfRequest)context.Services.GetService(typeof(PerfRequest))!).Author);
}

public sealed record PerfRequest(string Author);

[Userdata("perf_author")]
public partial class PerfAuthor(string name)
{
    [Prop("name", ReadOnly = true)]
    public string Name { get; } = name;
}

[Module("perf_async")]
public partial class PerfAsyncModule
{
    [Fn("add_sync")]
    public static long AddSync(long left, long right) => left + right;

    [Fn("add_async", Async = true)]
    public static ValueTask<long> AddAsync(long left, long right)
        => ValueTask.FromResult(left + right);

    [Fn("add_suspended", Async = true)]
    public static async ValueTask<long> AddSuspended(long left, long right)
    {
        await Task.Yield();
        return left + right;
    }

    [Fn("fault_completed", Async = true)]
    public static ValueTask<long> FaultCompleted()
        => new(Task.FromException<long>(new InvalidOperationException("benchmark fault")));
}

[Module("perf_async_user")]
public partial class PerfAsyncUserModule
{
    [Fn("new")]
    public static PerfAsyncUser New() => new();
}

[Userdata("perf_async_user")]
public partial class PerfAsyncUser
{
    [Fn("add_sync")]
    public long AddSync(long left, long right) => left + right;

    [Fn("add_async", Async = true)]
    public ValueTask<long> AddAsync(long left, long right)
        => ValueTask.FromResult(left + right);

    [Fn("add_suspended", Async = true)]
    public async ValueTask<long> AddSuspended(long left, long right)
    {
        await Task.Yield();
        return left + right;
    }
}

[MemoryDiagnoser]
public class StartupBenchmarks
{
    [Benchmark]
    public async Task PopEngineAndTrustedSession()
    {
        await using var runtime = Engine.Create();
        await using var session = runtime.Session(Sandbox.Trusted);
    }

    [Benchmark]
    public void KeraStateWithLibraries()
    {
        using var lua = new KeraLua.Lua(openLibs: true);
    }
}

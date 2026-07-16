using System.Runtime.InteropServices;

namespace PopLua.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public async Task RunReturnsTypedValue()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<long>("return 1 + 1");

        Assert.True(result.Ok);
        Assert.Equal(2, result.Unwrap());
    }

    [Fact]
    public void RuntimeBuilderRejectsNullServicesAndDiagnostics()
    {
        var builder = new EngineBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.Services(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Diagnostics(null!));
    }

    [Fact]
    public async Task TypeMismatchReturnsFailedResult()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<long>("return 'hello'");

        Assert.False(result.Ok);
        Assert.IsType<NativeTypeException>(result.Error);
    }

    [Fact]
    public async Task ScriptErrorReturnsFailedResult()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("error('boom')");

        Assert.False(result.Ok);
        Assert.IsType<ScriptException>(result.Error);
        Assert.Contains("boom", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeErrorIncludesChunkLineAndTraceback()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run(Chunk.Code("""
            local function inner()
                local player = nil
                return player.name
            end

            local function main()
                return inner()
            end

            return main()
            """, name: "plugin:on_start.lua"));

        var error = Assert.IsType<ScriptException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal("plugin:on_start.lua", error.Chunk);
        Assert.Equal(3, error.Line);
        Assert.Contains("plugin:on_start.lua:3", error.Message, StringComparison.Ordinal);
        Assert.Contains("stack traceback", error.LuaTrace, StringComparison.Ordinal);
        Assert.Contains("plugin:on_start.lua", error.LuaTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompileErrorIncludesChunkAndLine()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Trusted);
        var error = Assert.Throws<ScriptException>(() =>
            session.Compile(Chunk.Code("return =", name: "broken.lua")));

        Assert.Equal("broken.lua", error.Chunk);
        Assert.Equal(1, error.Line);
        Assert.Contains("broken.lua:1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallEmitsStartedAndFailedDiagnostics()
    {
        var diagnostics = new RuntimeRecordingDiagnostics();
        var lua = Engine.Create(b => b.Diagnostics(diagnostics));

        await using var session = lua.Session(Sandbox.Trusted, Identity.Create("plugin-1", "Plugin One"));
        var result = await session.Call("missing_function");

        Assert.False(result.Ok);
        Assert.Equal(1, diagnostics.StartedCount);
        Assert.Equal(1, diagnostics.FailedCount);
        Assert.Equal("call:missing_function", diagnostics.LastStartedChunk?.Name);
        Assert.Equal("plugin-1", diagnostics.LastStartedContext?.Identity.Id);
        Assert.Same(result.Error, diagnostics.LastError);
    }

    [Fact]
    public async Task InstructionQuotaStopsInfiniteLoop()
    {
        var lua = Engine.Create();
        var sandbox = Sandbox.Build(b => b.Quota(instructions: 1_000, hookInterval: 100));

        await using var session = lua.Session(sandbox);
        var result = await session.Run("while true do end");

        Assert.False(result.Ok);
        Assert.IsType<QuotaException>(result.Error);
    }

    [Fact]
    public async Task ActiveTimeQuotaStopsInfiniteLoop()
    {
        var lua = Engine.Create();
        var sandbox = Sandbox.Build(b => b.Quota(activeTime: TimeSpan.FromMilliseconds(1), hookInterval: 100));

        await using var session = lua.Session(sandbox);
        var result = await session.Run("while true do end");

        var error = Assert.IsType<QuotaException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal(QuotaKind.ActiveTime, error.Kind);
    }

    [Fact]
    public async Task WallTimeQuotaStopsActiveLuaExecution()
    {
        var lua = Engine.Create();
        var sandbox = Sandbox.Build(b => b.Quota(wallTime: TimeSpan.FromMilliseconds(1), hookInterval: 100));

        await using var session = lua.Session(sandbox);
        var result = await session.Run("while true do end");

        var error = Assert.IsType<QuotaException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal(QuotaKind.WallTime, error.Kind);
    }

    [Fact]
    public async Task CancellationStopsActiveLuaExecution()
    {
        var lua = Engine.Create();
        using var cts = new CancellationTokenSource();

        await using var session = lua.Session(Sandbox.Trusted);
        var run = Task.Run(async () => await session.Run("while true do end", cts.Token));

        await Task.Delay(TimeSpan.FromMilliseconds(50));
        cts.Cancel();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Ok);
        Assert.IsType<ScriptException>(result.Error);
        Assert.Contains("canceled", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UntrustedSessionDoesNotOpenLibs()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run<bool>("return math == nil and string == nil and table == nil and pcall == nil");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task SafeLibsOpenNativeSafeProfile()
    {
        var lua = Engine.Create();
        var sandbox = Sandbox.Build(b => b.AllowSafeLibs());

        await using var session = lua.Session(sandbox);
        var result = await session.Run<bool>("""
            local values = { "a", "b" }
            table.insert(values, "c")
            local ipairs_count = 0
            for _, _ in ipairs(values) do
                ipairs_count = ipairs_count + 1
            end
            local pairs_count = 0
            for _, _ in pairs({ a = 1, b = 2 }) do
                pairs_count = pairs_count + 1
            end

            return math.max(2, 3) == 3
                and string.upper("ok") == "OK"
                and utf8.len("serene") == 6
                and #values == 3
                and ipairs_count == 3
                and pairs_count == 2
                and type(pcall) == "function"
                and type(error) == "function"
                and io == nil
                and os == nil
                and package == nil
                and debug == nil
                and load == nil
                and dofile == nil
                and collectgarbage == nil
                and print == nil
            """);

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task SelectedLibsOpenOnlyRequestedLibs()
    {
        var lua = Engine.Create();
        var sandbox = Sandbox.Build(b => b.AllowLibs(Library.Math));

        await using var session = lua.Session(sandbox);
        var result = await session.Run<bool>("return math.max(1, 2) == 2 and string == nil and pcall == nil");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task TrustedSessionStillOpensFullLibs()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<bool>("""
            return type(math) == "table"
                and type(string) == "table"
                and type(table) == "table"
                and type(io) == "table"
                and type(os) == "table"
                and type(package) == "table"
                and type(debug) == "table"
                and type(load) == "function"
            """);

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task ControlledRequireRemainsSeparateFromPackageLib()
    {
        var lua = Engine.Create(b => b.Require((_, name) =>
            name == "util"
                ? Chunk.Code("return { value = function() return math.max(40, 42) end }", "module:util.lua")
                : null));
        var sandbox = Sandbox.Build(b => b.AllowSafeLibs());

        await using var session = lua.Session(sandbox);
        var result = await session.Run<bool>("return package == nil and require('util').value() == 42");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task QuotaFailureEmitsDiagnostics()
    {
        var diagnostics = new RecordingDiagnostics();
        var lua = Engine.Create(b => b.Diagnostics(diagnostics));
        var sandbox = Sandbox.Build(b => b.Quota(instructions: 1_000, hookInterval: 100));

        await using var session = lua.Session(sandbox);
        var result = await session.Run("while true do end");

        Assert.False(result.Ok);
        Assert.Equal(1, diagnostics.FailedCount);
        Assert.Equal(1, diagnostics.QuotaBlockedCount);
        Assert.Equal(QuotaKind.Instructions, diagnostics.LastQuotaKind);
    }

    [Fact]
    public async Task CallDepthQuotaStopsRecursion()
    {
        var lua = Engine.Create();
        var sandbox = Sandbox.Build(b => b.Quota(callDepth: 8));

        await using var session = lua.Session(sandbox);
        var result = await session.Run("""
            local function recurse(n)
                if n == 0 then
                    return 1
                end

                local value = recurse(n - 1)
                return value
            end

            return recurse(100)
            """);

        var error = Assert.IsType<QuotaException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal(QuotaKind.CallDepth, error.Kind);
    }

    [Fact]
    public async Task CallDepthQuotaFailureEmitsDiagnostics()
    {
        var diagnostics = new RecordingDiagnostics();
        var lua = Engine.Create(b => b.Diagnostics(diagnostics));
        var sandbox = Sandbox.Build(b => b.Quota(callDepth: 4));

        await using var session = lua.Session(sandbox);
        var result = await session.Run("""
            local function recurse(n)
                local value = recurse(n + 1)
                return value
            end

            return recurse(1)
            """);

        Assert.False(result.Ok);
        Assert.Equal(1, diagnostics.FailedCount);
        Assert.Equal(1, diagnostics.QuotaBlockedCount);
        Assert.Equal(QuotaKind.CallDepth, diagnostics.LastQuotaKind);
    }

    [Fact]
    public async Task MemoryQuotaStopsAllocationAndDoesNotPoisonLaterRun()
    {
        var lua = Engine.Create();
        var sandbox = Sandbox.Build(b => b.Memory(heapBytes: 512 * 1024));

        await using var session = lua.Session(sandbox);
        var failed = await session.Run("""
            local values = {}
            for i = 1, 100000 do
                values[i] = { i, i, i, i, i, i, i, i }
            end

            return #values
            """);

        var succeeded = await session.Run<long>("return 42");

        var error = Assert.IsType<QuotaException>(failed.Error);
        Assert.False(failed.Ok);
        Assert.Equal(QuotaKind.Memory, error.Kind);
        Assert.True(succeeded.Ok);
        Assert.Equal(42, succeeded.Unwrap());
    }

    [Fact]
    public async Task GcThresholdTracksPeakMemoryAndCallDepthMetrics()
    {
        var diagnostics = new RecordingDiagnostics();
        var lua = Engine.Create(b => b.Diagnostics(diagnostics));
        var sandbox = Sandbox.Build(b => b
            .Quota(callDepth: 32, hookInterval: 100)
            .Memory(gcBytes: 64 * 1024));

        await using var session = lua.Session(sandbox);
        var result = await session.Run<long>("""
            local function wrap(value)
                local values = {}
                for i = 1, 1000 do
                    values[i] = { value, value, value, value }
                end

                return value
            end

            return wrap(7)
            """);

        Assert.True(result.Ok);
        Assert.NotNull(diagnostics.LastCompletedMetrics);
        Assert.True(diagnostics.LastCompletedMetrics.Value.PeakMemoryBytes > 0);
        Assert.True(diagnostics.LastCompletedMetrics.Value.MaxCallDepth >= 1);
    }

    [Fact]
    public async Task CallInvokesGlobalFunction()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Trusted);
        (await session.Run("function add(a, b) return a + b end")).ThrowIfError();

        var result = await session.Call<long>("add", Value.From(3L), Value.From(4L));

        Assert.True(result.Ok);
        Assert.Equal(7, result.Unwrap());
    }

    [Fact]
    public async Task DisposedSessionThrows()
    {
        var lua = Engine.Create();
        var session = lua.Session(Sandbox.Trusted);

        await session.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await session.Run("return 1"));
    }

    [Fact]
    public async Task ModuleWithoutCapabilityIsNotRegistered()
    {
        var lua = Engine.Create(b => b.Module<HiddenModule>());

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run<bool>("return hidden == nil");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task BytecodeCanRunMultipleTimes()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Trusted);
        var bytecode = session.Compile("return 6 * 7");

        Assert.Equal(42, (await session.Run<long>(bytecode)).Unwrap());
        Assert.Equal(42, (await session.Run<long>(bytecode)).Unwrap());
    }

    [Fact]
    public async Task BytecodeCanRunAcrossExecutionSessions()
    {
        var lua = Engine.Create();
        Bytecode bytecode;

        await using (var compileSession = lua.Session(Sandbox.Trusted))
        {
            bytecode = compileSession.Compile(Chunk.Code("""
                calls = (calls or 0) + 1
                """, name: "plugin:on_start.lua"));
        }

        await using var first = lua.Session(Sandbox.Trusted);
        await using var second = lua.Session(Sandbox.Trusted);

        Assert.True((await first.Run(bytecode)).Ok);
        Assert.True((await second.Run(bytecode)).Ok);
        Assert.Equal(1, (await first.Run<long>("return calls")).Unwrap());
        Assert.Equal(1, (await second.Run<long>("return calls")).Unwrap());
    }

    [Module("hidden", Cap = Caps.FileRead)]
    public partial class HiddenModule
    {
        [Fn("value")]
        public static long Value() => 123;
    }

    private sealed class RuntimeRecordingDiagnostics : IDiagnostics
    {
        public int StartedCount { get; private set; }
        public int FailedCount { get; private set; }
        public ScriptContext? LastStartedContext { get; private set; }
        public Chunk? LastStartedChunk { get; private set; }
        public RuntimeException? LastError { get; private set; }

        public void Started(ScriptContext ctx, Chunk chunk)
        {
            StartedCount++;
            LastStartedContext = ctx;
            LastStartedChunk = chunk;
        }

        public void Completed(ScriptContext ctx, in Metrics metrics)
        {
        }

        public void Failed(ScriptContext ctx, RuntimeException error)
        {
            FailedCount++;
            LastError = error;
        }

        public void QuotaBlocked(ScriptContext ctx, QuotaKind kind)
        {
        }

        public void SandboxBlocked(ScriptContext ctx, string cap)
        {
        }
    }
}

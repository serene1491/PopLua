namespace PopLua.Tests;

public sealed class ControlledLoadingTests
{
    [Fact]
    public async Task RequireIsAbsentWhenNoResolverIsConfiguredInUntrustedSession()
    {
        var lua = Engine.Create();

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run<bool>("return require == nil");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task RequireLoadsModuleAndCachesReturnedTablePerSession()
    {
        var resolveCount = 0;
        var lua = Engine.Create(b => b.Require((_, name) =>
        {
            if (name != "util")
                return null;

            resolveCount++;
            return Chunk.Code("""
                return {
                    message = function()
                        return "hello"
                    end
                }
                """, name: "module:util.lua");
        }));

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run<string>("""
            local first = require("util")
            local second = require("util")
            if first ~= second then
                return "not cached"
            end

            return first.message()
            """);

        Assert.True(result.Ok);
        Assert.Equal("hello", result.Unwrap());
        Assert.Equal(1, resolveCount);
    }

    [Fact]
    public async Task RequiredModuleReturningNilIsCachedAsTrue()
    {
        var resolveCount = 0;
        var lua = Engine.Create(b => b.Require((_, name) =>
        {
            if (name != "empty")
                return null;

            resolveCount++;
            return Chunk.Code("return nil", name: "module:empty.lua");
        }));

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run<bool>("return require('empty') == true and require('empty') == true");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
        Assert.Equal(1, resolveCount);
    }

    [Fact]
    public async Task MissingModuleFailsClearlyAndIsNotCached()
    {
        var resolveCount = 0;
        var lua = Engine.Create(b => b.Require((_, name) =>
        {
            if (name == "missing")
                resolveCount++;

            return null;
        }));

        await using var session = lua.Session(Sandbox.Untrusted);
        var first = await session.Run("require('missing')");
        var second = await session.Run("require('missing')");

        Assert.False(first.Ok);
        Assert.Contains("module not found: missing", first.Error!.Message, StringComparison.Ordinal);
        Assert.False(second.Ok);
        Assert.Contains("module not found: missing", second.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(2, resolveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../util")]
    [InlineData("./util")]
    [InlineData("/util")]
    [InlineData("C:\\util")]
    [InlineData("util.lua")]
    [InlineData("util..math")]
    [InlineData("util-math")]
    public async Task InvalidModuleNamesFailBeforeResolverRuns(string moduleName)
    {
        var resolverCalled = false;
        var lua = Engine.Create(b => b.Require((_, _) =>
        {
            resolverCalled = true;
            return null;
        }));

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run($"require({Literal(moduleName)})");

        Assert.False(result.Ok);
        Assert.False(resolverCalled);
        Assert.Contains("invalid module name", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolverExceptionFailsClearly()
    {
        var lua = Engine.Create(b => b.Require((ctx, name) =>
        {
            if (ctx.Services.GetService(typeof(ModuleCatalog)) is not ModuleCatalog)
                throw new InvalidOperationException("module catalog service is missing");

            return Chunk.Code("return true", name: $"module:{name}.lua");
        }));

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run("return require('util')");

        Assert.False(result.Ok);
        Assert.Contains(
            "module resolver failed: util: module catalog service is missing",
            result.Error!.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidModuleSourcePreservesChunkName()
    {
        var lua = Engine.Create(b => b.Require((_, name) =>
            name == "broken"
                ? Chunk.Code("return {", name: "module:broken.lua")
                : null));

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run("return require('broken')");

        Assert.False(result.Ok);
        Assert.Contains("module:broken.lua:1", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ModuleRuntimeErrorPreservesModuleChunkNameAndTraceback()
    {
        var lua = Engine.Create(b => b.Require((_, name) =>
            name == "bad"
                ? Chunk.Code("""
                    local value = nil
                    return value.name
                    """, name: "module:bad.lua")
                : null));

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return require('bad')");

        var error = Assert.IsType<ScriptException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal("module:bad.lua", error.Chunk);
        Assert.Equal(2, error.Line);
        Assert.Contains("module:bad.lua:2", error.Message, StringComparison.Ordinal);
        Assert.Contains("module:bad.lua", error.LuaTrace, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LuaPCallCatchesMissingModuleWhenLuaErrorIsAvailable()
    {
        var lua = Engine.Create(b => b.Require((_, _) => null));

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<bool>("local ok = pcall(require, 'missing'); return ok == false");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task CyclicModuleLoadFailsClearly()
    {
        var lua = Engine.Create(b => b.Require((_, name) =>
            name == "cycle"
                ? Chunk.Code("return require('cycle')", name: "module:cycle.lua")
                : null));

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return require('cycle')");

        Assert.False(result.Ok);
        Assert.Contains("cyclic module load: cycle -> cycle", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NestedCyclicModuleLoadReportsModuleChain()
    {
        var lua = Engine.Create(b => b.Require((_, name) =>
            name switch
            {
                "a" => Chunk.Code("return require('b')", name: "module:a.lua"),
                "b" => Chunk.Code("return require('c')", name: "module:b.lua"),
                "c" => Chunk.Code("return require('a')", name: "module:c.lua"),
                _ => null,
            }));

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return require('a')");

        Assert.False(result.Ok);
        Assert.Contains("cyclic module load: a -> b -> c -> a", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedModuleLoadDoesNotPoisonLaterSuccessfulLoad()
    {
        var fail = true;
        var lua = Engine.Create(b => b.Require((_, name) =>
        {
            if (name != "flaky")
                return null;

            return fail
                ? Chunk.Code("return require('missing')", name: "module:flaky.lua")
                : Chunk.Code("return { value = function() return 42 end }", name: "module:flaky.lua");
        }));

        await using var session = lua.Session(Sandbox.Untrusted);
        var failed = await session.Run("return require('flaky')");
        fail = false;
        var succeeded = await session.Run<long>("return require('flaky').value()");

        Assert.False(failed.Ok);
        Assert.True(succeeded.Ok);
        Assert.Equal(42, succeeded.Unwrap());
    }

    [Fact]
    public async Task RequiredModuleCanCallGeneratedHostFunctionsInUntrustedSession()
    {
        var lua = Engine.Create(b => b
            .Module<GeneratedMathModule>()
            .Require((_, name) => name == "calc"
                ? Chunk.Code("return { value = function() return mathx.add(20, 22) end }", name: "module:calc.lua")
                : null));

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run<long>("return require('calc').value()");

        Assert.True(result.Ok);
        Assert.Equal(42, result.Unwrap());
    }

    [Fact]
    public async Task LuaPCallCatchesModuleRuntimeFailureWhenLuaErrorIsAvailable()
    {
        var lua = Engine.Create(b => b.Require((_, name) =>
            name == "bad"
                ? Chunk.Code("""
                    local value = nil
                    return value.name
                    """, name: "module:bad.lua")
                : null));

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<bool>("""
            local ok, message = pcall(function()
                return require('bad')
            end)

            return ok == false and string.find(message, 'module:bad.lua:2', 1, true) ~= nil
            """);

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task UntrustedModuleRuntimeFailureUsesResultError()
    {
        var lua = Engine.Create(b => b.Require((_, name) =>
            name == "bad"
                ? Chunk.Code("""
                    local value = nil
                    return value.name
                    """, name: "module:bad.lua")
                : null));

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run("return require('bad')");

        Assert.False(result.Ok);
        Assert.Contains("module:bad.lua:2", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionServiceResolverOverridesRuntimeResolverAndCacheIsPerSession()
    {
        var runtimeCount = 0;
        var serviceCount = 0;
        var lua = Engine.Create(b => b.Require((_, name) =>
        {
            runtimeCount++;
            return Chunk.Code($"return {{ value = function() return 'runtime:{name}' end }}", name: $"module:{name}.lua");
        }));
        var services = Services.Create().Add<ModuleResolver>((_, name) =>
        {
            serviceCount++;
            return Chunk.Code($"return {{ value = function() return 'service:{name}' end }}", name: $"module:{name}.lua");
        });

        await using var first = lua.Session(Sandbox.Untrusted);
        await using var second = lua.Session(Sandbox.Untrusted, services: services);

        var firstResult = await first.Run<string>("return require('util').value()");
        var secondResult = await second.Run<string>("return require('util').value()");

        Assert.True(firstResult.Ok);
        Assert.True(secondResult.Ok);
        Assert.Equal("runtime:util", firstResult.Unwrap());
        Assert.Equal("service:util", secondResult.Unwrap());
        Assert.Equal(1, runtimeCount);
        Assert.Equal(1, serviceCount);
    }

    [Fact]
    public async Task RequiredModuleCanSuspendDuringInitialization()
    {
        var service = new AsyncApiService();
        var services = Services.Create().Add(service);
        var lua = Engine.Create(b => b
            .Module<AsyncApiModule>()
            .Require((_, name) => name == "async_init"
                ? Chunk.Code("return { value = async_api.delayed('module') }", name: "module:async_init.lua")
                : null));

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var run = session.Run<string>("return require('async_init').value").AsTask();

        await service.WaitForStartedCountAsync(1);
        service.Complete("ready");

        var result = await run.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.Ok);
        Assert.Equal("module:ready", result.Unwrap());
    }

    [Fact]
    public async Task FailedAsyncModuleInitializationDoesNotPoisonLaterSuccessfulLoad()
    {
        var fail = true;
        var service = new AsyncApiService();
        var services = Services.Create().Add(service);
        var lua = Engine.Create(b => b
            .Module<AsyncApiModule>()
            .Require((_, name) =>
            {
                if (name != "async_flaky")
                    return null;

                return fail
                    ? Chunk.Code("return { value = async_api.delayed('bad') }", name: "module:async_flaky.lua")
                    : Chunk.Code("return { value = 'ok' }", name: "module:async_flaky.lua");
            }));

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var failedRun = session.Run("return require('async_flaky')").AsTask();
        await service.WaitForStartedCountAsync(1);
        service.Fail(new InvalidOperationException("async init failed"));

        var failed = await failedRun.WaitAsync(TimeSpan.FromSeconds(2));
        fail = false;
        var succeeded = await session.Run<string>("return require('async_flaky').value");

        Assert.False(failed.Ok);
        Assert.Contains("async init failed", failed.Error!.Message, StringComparison.Ordinal);
        Assert.True(succeeded.Ok);
        Assert.Equal("ok", succeeded.Unwrap());
    }

    private static string Literal(string value)
        => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private sealed class ModuleCatalog
    {
    }
}

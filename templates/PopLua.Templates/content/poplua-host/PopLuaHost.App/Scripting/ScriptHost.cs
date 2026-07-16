using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;
using PopLuaHost.Scripting;

namespace PopLuaHost.App.Scripting;

internal sealed class ScriptHost
{
    private readonly Engine _lua;
    private readonly Sandbox _sandbox;
    private readonly ScriptCatalog _scripts;
    private readonly ScriptLog _log;
    private readonly HostDiagnostics _diagnostics;

    private ScriptHost(
        Engine lua,
        Sandbox sandbox,
        ScriptCatalog scripts,
        ScriptLog log,
        HostDiagnostics diagnostics)
    {
        _lua = lua;
        _sandbox = sandbox;
        _scripts = scripts;
        _log = log;
        _diagnostics = diagnostics;
    }

    public static ScriptHost CreateDefault()
    {
        var diagnostics = new HostDiagnostics();
        var log = new ScriptLog();
        var scripts = ScriptCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Scripts"));

        var lua = Engine.Create(builder => builder
            .Modules(ScriptingRegistration.RegisterAll)
            .Diagnostics(diagnostics)
            .Require(scripts.ResolveModule));

        var sandbox = Sandbox.Build(builder => builder
            .AllowSafeLibs()
            .Allow(ScriptCaps.Host)
            .Allow(ScriptCaps.Log)
            .Quota(
                instructions: 100_000,
                activeTime: TimeSpan.FromSeconds(1),
                wallTime: TimeSpan.FromSeconds(30),
                callDepth: 64));

        return new ScriptHost(lua, sandbox, scripts, log, diagnostics);
    }

    public async Task<Result<long>> Run(string id, string name)
    {
        var identity = Identity.Create(id, name);

        await using var compileSession = _lua.Session(_sandbox, identity);
        var bytecode = compileSession.Compile(_scripts.Entrypoint($"{id}:main.lua"));

        var services = Services.Create().Add<IScriptLog>(_log);
        await using var runSession = _lua.Session(_sandbox, identity, services);
        return await runSession.Run<long>(bytecode);
    }

    public void PrintLog()
    {
        foreach (var entry in _log.Entries)
            Console.WriteLine(entry);
    }

    public void PrintDiagnostics()
    {
        foreach (var entry in _diagnostics.Events)
            Console.WriteLine(entry);
    }
}

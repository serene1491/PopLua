using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace PopLuaHost.App.Scripting;

internal sealed class HostDiagnostics : IRunObserver
{
    private readonly List<string> _events = [];

    public IReadOnlyList<string> Events => _events;

    public void Started(ScriptContext context, Step step)
        => _events.Add($"started {context.Run.Id}:{step.Id}:{step.Name}");

    public void Completed(ScriptContext context, Step step, in Metrics metrics)
        => _events.Add($"completed {step.Id}: active={metrics.Active.TotalMilliseconds:F1}ms, suspended={metrics.Suspended.TotalMilliseconds:F1}ms");

    public void Failed(ScriptContext context, Step step, RuntimeException error)
        => _events.Add($"failed {step.Id}: {error.Message}");

    public void Quota(ScriptContext context, Step step, QuotaKind kind)
        => _events.Add($"quota {step.Id}: {kind}");

    public void Denied(ScriptContext context, Step step, string capability)
        => _events.Add($"sandbox {step.Id}: {capability}");
}

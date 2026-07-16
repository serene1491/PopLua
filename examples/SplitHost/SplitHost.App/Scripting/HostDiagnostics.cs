using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace SplitHost.App.Scripting;

internal sealed class HostDiagnostics : IDiagnostics
{
    private readonly List<string> _events = [];

    public IReadOnlyList<string> Events => _events;

    public void Started(ScriptContext context, Chunk chunk)
        => _events.Add($"started {context.Identity.Id}:{chunk.Name}");

    public void Completed(ScriptContext context, in Metrics metrics)
        => _events.Add($"completed {context.Identity.Id}: {metrics.Duration.TotalMilliseconds:F1}ms, instructions={metrics.Instructions}");

    public void Failed(ScriptContext context, RuntimeException error)
        => _events.Add($"failed {context.Identity.Id}: {error.Message}");

    public void QuotaBlocked(ScriptContext context, QuotaKind kind)
        => _events.Add($"quota {context.Identity.Id}: {kind}");

    public void SandboxBlocked(ScriptContext context, string capability)
        => _events.Add($"sandbox {context.Identity.Id}: {capability}");
}

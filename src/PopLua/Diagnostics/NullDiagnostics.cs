namespace PopLua.Diagnostics;

/// <summary>
/// No-op diagnostics collector.
/// </summary>
public sealed class NullDiagnostics : IDiagnostics
{
    /// <summary>
    /// Gets a diagnostics sink that ignores all execution events.
    /// </summary>
    public static NullDiagnostics Instance { get; } = new();

    private NullDiagnostics()
    {
    }

    /// <inheritdoc />
    public void Started(ScriptContext ctx, Chunk chunk)
    {
    }

    /// <inheritdoc />
    public void Completed(ScriptContext ctx, in Metrics metrics)
    {
    }

    /// <inheritdoc />
    public void Failed(ScriptContext ctx, RuntimeException error)
    {
    }

    /// <inheritdoc />
    public void QuotaBlocked(ScriptContext ctx, QuotaKind kind)
    {
    }

    /// <inheritdoc />
    public void SandboxBlocked(ScriptContext ctx, string cap)
    {
    }
}

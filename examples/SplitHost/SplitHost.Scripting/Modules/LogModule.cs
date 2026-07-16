using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace SplitHost.Scripting.Modules;

/// <summary>Host-controlled script output.</summary>
[Module("log", Cap = ScriptCaps.Log)]
public partial class LogModule(IScriptLog log)
{
    /// <summary>Writes an informational script log message.</summary>
    /// <param name="ctx">The active Lua execution context.</param>
    /// <param name="message">Message supplied by the script.</param>
    [Fn("info")]
    public void Info([Context] ScriptContext ctx, string message)
        => Write(ctx, ScriptLogLevel.Info, message);

    /// <summary>Writes a warning script log message.</summary>
    /// <param name="ctx">The active Lua execution context.</param>
    /// <param name="message">Message supplied by the script.</param>
    [Fn("warn")]
    public void Warn([Context] ScriptContext ctx, string message)
        => Write(ctx, ScriptLogLevel.Warn, message);

    /// <summary>Writes an error script log message.</summary>
    /// <param name="ctx">The active Lua execution context.</param>
    /// <param name="message">Message supplied by the script.</param>
    [Fn("error")]
    public void Error([Context] ScriptContext ctx, string message)
        => Write(ctx, ScriptLogLevel.Error, message);

    private void Write(ScriptContext ctx, ScriptLogLevel level, string message)
    {
        ctx.Sandbox.Require(ScriptCaps.Log);
        log.Write(ctx, level, message);
    }
}

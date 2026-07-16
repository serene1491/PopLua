using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace PopLuaHost.Scripting;

[Module("log", Cap = ScriptCaps.Log)]
public partial class LogModule(IScriptLog log)
{
    [Fn("info")]
    public void Info([Context] ScriptContext ctx, string message)
        => log.Write(ctx, ScriptLogLevel.Info, message);

    [Fn("warn")]
    public void Warn([Context] ScriptContext ctx, string message)
        => log.Write(ctx, ScriptLogLevel.Warn, message);

    [Fn("error")]
    public void Error([Context] ScriptContext ctx, string message)
        => log.Write(ctx, ScriptLogLevel.Error, message);
}

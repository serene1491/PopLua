using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace PopLuaHost.Scripting;

public enum ScriptLogLevel
{
    Info,
    Warn,
    Error,
}

public interface IScriptLog
{
    void Write(ScriptContext context, ScriptLogLevel level, string message);
}

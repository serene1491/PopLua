using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;
using PopLuaHost.Scripting;

namespace PopLuaHost.App.Scripting;

internal sealed class ScriptLog : IScriptLog
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public void Write(ScriptContext context, ScriptLogLevel level, string message)
        => _entries.Add($"[{context.Identity.Id}] {level}: {message}");
}

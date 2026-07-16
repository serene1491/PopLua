using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;
using SplitHost.Scripting.Modules;

namespace SplitHost.Scripting;

public static class ScriptingRegistration
{
    public static void RegisterAll(ModuleCollection modules)
    {
        modules.Add<HostModule>();
        modules.Add<LogModule>();
    }
}

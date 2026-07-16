using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace PopLuaHost.Scripting;

[Module("host", Cap = ScriptCaps.Host)]
public partial class HostModule
{
    [Fn("identity")]
    public static string Identity([Context] ScriptContext ctx)
        => ctx.Identity.Name ?? ctx.Identity.Id;

    [Fn("player")]
    public static Player Player(string name) => new(name, score: 7);
}

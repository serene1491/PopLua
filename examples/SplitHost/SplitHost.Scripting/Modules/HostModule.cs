using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;
using SplitHost.Scripting.Userdata;

namespace SplitHost.Scripting.Modules;

/// <summary>Small host API exposed to scripts.</summary>
[Module("host", Cap = ScriptCaps.Host)]
public partial class HostModule
{
    /// <summary>Gets the current script identity display name.</summary>
    /// <param name="ctx">The active Lua execution context.</param>
    /// <returns>The identity name, or the identity id when no display name is set.</returns>
    [Fn("identity")]
    public static string Identity([Context] ScriptContext ctx)
        => ctx.Identity.Name ?? ctx.Identity.Id;

    /// <summary>Creates a sample player userdata value.</summary>
    /// <param name="name">Player name.</param>
    /// <returns>A new player userdata value.</returns>
    [Fn("player")]
    public static Player Player(string name) => new(name);
}

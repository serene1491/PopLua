using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace PopLuaHost.Scripting;

[Userdata("player")]
public partial class Player(string name, long score)
{
    [Prop("score", ReadOnly = true)]
    public long Score { get; } = score;

    [Fn("name")]
    public string Name() => name;
}

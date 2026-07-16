using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace PopMarshallers.Modules;

[Module("host")]
public partial class HostModule
{
    [Const("version")]
    public const string Version = "1.0";

    [Fn("greet")]
    public static string Greet(string name) => $"hello, {name}";

    [Fn("player")]
    public static Player Player(string name) => new(name, score: 7);
}

using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var services = Services.Create().Add(new HostContext("Pop"));
var lua = Engine.Create(b => b.Module<ContextModule>());

await using var session = lua.Session(Sandbox.Untrusted, services: services);

var result = await session.Run<string>("""
    ctx.reply("hello")
    return ctx.author.username .. ": " .. ctx.last_reply
    """);

Console.WriteLine(result.Unwrap());

/// <summary>
/// Per-execution host context exposed as a generated root module.
/// </summary>
[Module("ctx")]
public partial class ContextModule(HostContext context)
{
    [Prop("author")]
    public HostUser Author() => new(context.User);

    [Prop("last_reply")]
    public string LastReply() => context.LastReply;

    [Fn("reply")]
    public void Reply(string message) => context.LastReply = message;
}

public sealed class HostContext(string user)
{
    public string User { get; } = user;
    public string LastReply { get; set; } = "";
}

[Userdata("host_user")]
public partial class HostUser(string username)
{
    [Prop("username", ReadOnly = true)]
    public string Username { get; } = username;
}

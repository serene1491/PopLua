using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var handler = new EventScriptHost(new ConsoleResponder());

await handler.Handle(new AppEvent(
    Id: "evt-1",
    Type: "signup",
    Actor: "user-42",
    Data: new Dictionary<string, string> { ["plan"] = "pro" }), """
    if event.type() == "signup" then
        respond.send("Welcome " .. event.actor())
    end
    """);

public sealed class EventScriptHost(IResponder responder)
{
    private readonly Engine _lua = Engine.Create(b => b
        .Module<EventModule>()
        .Module<RespondModule>());

    private readonly Sandbox _sandbox = Sandbox.Build(b => b.Allow(EventCaps.Respond));

    public async Task Handle(AppEvent evt, string script)
    {
        var services = Services.Create()
            .Add(evt)
            .Add<IResponder>(responder);

        await using var session = _lua.Session(_sandbox, services: services);
        (await session.Run(script)).ThrowIfError();
    }
}

[Module("event")]
public partial class EventModule(AppEvent evt)
{
    [Fn("id")]
    public string Id() => evt.Id;

    [Fn("type")]
    public string Type() => evt.Type;

    [Fn("actor")]
    public string Actor() => evt.Actor;

    [Fn("data")]
    public string Data(string key)
        => evt.Data.TryGetValue(key, out var value) ? value : "";
}

[Module("respond")]
public partial class RespondModule(IResponder responder)
{
    [Fn("send", Async = true)]
    public ValueTask Send([Context] ScriptContext ctx, string message)
    {
        ctx.Sandbox.Require(EventCaps.Respond);
        return responder.Send(message, ctx.Cancellation);
    }
}

public sealed record AppEvent(
    string Id,
    string Type,
    string Actor,
    IReadOnlyDictionary<string, string> Data);

public interface IResponder
{
    ValueTask Send(string message, CancellationToken ct);
}

public sealed class ConsoleResponder : IResponder
{
    public ValueTask Send(string message, CancellationToken ct)
    {
        Console.WriteLine(message);
        return ValueTask.CompletedTask;
    }
}

public static class EventCaps
{
    public const string Respond = "event.respond";
}

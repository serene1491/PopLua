using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var services = Services.Create().Add(new ApiClient());

var lua = Engine.Create(b => b.Module<ApiModule>());

var sandbox = Sandbox.Build(b => b.Allow(ApiCaps.Fetch));

await using var session = lua.Session(sandbox, services: services);

var body = await session.Run<string>("""
    local user = api.get_user("42")
    return user
    """);

Console.WriteLine(body.Unwrap());

[Module("api")]
public partial class ApiModule(ApiClient client)
{
    [Fn("get_user", Async = true)]
    public ValueTask<string> GetUser([Context] ScriptContext ctx, string id)
    {
        ctx.Sandbox.Require(ApiCaps.Fetch);
        return client.GetUser(id, ctx.Cancellation);
    }
}

public static class ApiCaps
{
    public const string Fetch = "api.fetch";
}

public sealed class ApiClient
{
    public async ValueTask<string> GetUser(string id, CancellationToken ct)
    {
        await Task.Delay(50, ct);
        return $$"""{"id":"{{id}}","name":"Pombo"}""";
    }
}

namespace PopLua.Tests;

public sealed class ContextTests
{
    [Fact]
    public void AnonymousIdentityHasId()
        => Assert.False(string.IsNullOrWhiteSpace(Identity.Anonymous.Id));

    [Fact]
    public void ServicesResolveByExactType()
    {
        var service = new MyService();
        var services = Services.Create().Add(service);

        Assert.Same(service, services.GetService(typeof(MyService)));
    }

    [Fact]
    public void IdentityCopiesTags()
    {
        var identity = Identity.Create(
            "x",
            tags: new Dictionary<string, object> { ["env"] = "prod" });

        Assert.Equal("prod", identity.Tags["env"]);
    }

    [Fact]
    public void ContextUsesProvidedValues()
    {
        var identity = Identity.Create("user-1");
        var sandbox = Sandbox.Trusted;
        var services = Services.Create().Add(new MyService());

        var ctx = ScriptContext.Create(sandbox, identity, services);

        Assert.Same(identity, ctx.Identity);
        Assert.Same(sandbox, ctx.Sandbox);
        Assert.Same(services, ctx.Services);
    }

    private sealed class MyService;
}

namespace PopLua.Tests;

public sealed class SandboxTests
{
    [Fact]
    public void UntrustedDoesNotAllowFileRead()
        => Assert.False(Sandbox.Untrusted.Has(Caps.FileRead));

    [Fact]
    public void TrustedAllowsFileRead()
        => Assert.True(Sandbox.Trusted.Has(Caps.FileRead));

    [Fact]
    public void UntrustedOpensNoLibs()
        => Assert.Equal(Library.None, Sandbox.Untrusted.Libs);

    [Fact]
    public void TrustedOpensAllLibs()
        => Assert.Equal(Library.All, Sandbox.Trusted.Libs);

    [Fact]
    public void BuilderAllowsCapability()
    {
        var sandbox = Sandbox.Build(b => b.Allow(Caps.Net));

        Assert.True(sandbox.Has(Caps.Net));
    }

    [Fact]
    public void DenyOverridesAllowAll()
    {
        var sandbox = Sandbox.Build(b => b
            .AllowAll()
            .Deny(Caps.Process));

        Assert.False(sandbox.Has(Caps.Process));
        Assert.True(sandbox.Has(Caps.Net));
    }

    [Fact]
    public void LaterAllowRemovesDeny()
    {
        var sandbox = Sandbox.Build(b => b
            .AllowAll()
            .Deny(Caps.Process)
            .Allow(Caps.Process));

        Assert.True(sandbox.Has(Caps.Process));
    }

    [Fact]
    public void RequireThrowsWhenCapabilityIsMissing()
        => Assert.Throws<SandboxException>(() => Sandbox.Untrusted.Require(Caps.Net));

    [Fact]
    public void CustomCapabilitiesWork()
    {
        var sandbox = Sandbox.Build(b => b.Allow("myapp.admin"));

        Assert.True(sandbox.Has("myapp.admin"));
    }

    [Fact]
    public void BuilderAllowsSafeLibs()
    {
        var sandbox = Sandbox.Build(b => b.AllowSafeLibs());

        Assert.Equal(Library.Safe, sandbox.Libs);
    }

    [Fact]
    public void BuilderAllowsSelectedLibs()
    {
        var sandbox = Sandbox.Build(b => b.AllowLibs(Library.Math | Library.String));

        Assert.Equal(Library.Math | Library.String, sandbox.Libs);
    }

    [Fact]
    public void FullBaseOverridesSafeBase()
    {
        var sandbox = Sandbox.Build(b => b.AllowLibs(
            Library.SafeBase | Library.FullBase));

        Assert.Equal(Library.FullBase, sandbox.Libs);
    }

    [Fact]
    public void UnsupportedStdLibFlagsAreRejected()
    {
        var invalid = (Library)(1 << 20);

        Assert.Throws<ArgumentOutOfRangeException>(() => Sandbox.Build(b => b.AllowLibs(invalid)));
    }
}

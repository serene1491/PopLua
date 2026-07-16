namespace PopLua.Tests;

public sealed class AttributeTests
{
    [Fact]
    public void LuaModuleStoresNameAndCapability()
    {
        var attribute = new ModuleAttribute("mathx") { Cap = Caps.Debug };

        Assert.Equal("mathx", attribute.Name);
        Assert.Equal(Caps.Debug, attribute.Cap);
    }

    [Fact]
    public void LuaFunctionStoresNameAndAsyncFlag()
    {
        var attribute = new FnAttribute("fetch") { Async = true, PauseTime = true };

        Assert.Equal("fetch", attribute.Name);
        Assert.True(attribute.Async);
        Assert.True(attribute.PauseTime);
    }

    [Fact]
    public void LuaUserdataDefaultsToToStringAndGcEnabled()
    {
        var attribute = new UserdataAttribute("vec2");

        Assert.True(attribute.ToString);
        Assert.True(attribute.Gc);
    }
}

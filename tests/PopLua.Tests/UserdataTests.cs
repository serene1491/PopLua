namespace PopLua.Tests;

public sealed class UserdataTests
{
    [Fact]
    public async Task UserdataMethodCanBeCalledFromLua()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<double>("return vec.new(3, 4):length()");

        Assert.True(result.Ok);
        Assert.Equal(5, result.Unwrap(), precision: 12);
    }

    [Fact]
    public async Task UserdataMethodCanBeCalledWithExplicitReceiver()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<double>("local v = vec.new(3, 4); return v.length(v)");

        Assert.True(result.Ok);
        Assert.Equal(5, result.Unwrap(), precision: 12);
    }

    [Fact]
    public async Task UserdataPropertiesCanBeReadFromLua()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<double>("local v = vec.new(3, 4); return v.x + v.y");

        Assert.True(result.Ok);
        Assert.Equal(7, result.Unwrap(), precision: 12);
    }

    [Fact]
    public async Task UserdataReadonlyPropertySetFailsAsLuaError()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("local v = vec.new(3, 4); v.x = 5");

        Assert.False(result.Ok);
        Assert.IsType<ScriptException>(result.Error);
    }

    [Fact]
    public async Task UserdataOperatorWorksFromLua()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<double>("local c = vec.new(3, 4) + vec.new(1, 2); return c.x * 10 + c.y");

        Assert.True(result.Ok);
        Assert.Equal(46, result.Unwrap(), precision: 12);
    }

    [Fact]
    public async Task UserdataAllSupportedOperatorsWorkFromLua()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<bool>("""
            local a = vec.new(8, 4)
            local b = vec.new(2, 1)
            local sub = a - b
            local mul = a * b
            local div = a / b
            local neg = -b

            return sub.x == 6 and sub.y == 3
                and mul.x == 16 and mul.y == 4
                and div.x == 4 and div.y == 4
                and neg.x == -2 and neg.y == -1
                and vec.new(1, 1) == vec.new(1, 1)
                and vec.new(1, 1) < vec.new(3, 4)
                and vec.new(3, 4) <= vec.new(3, 4)
            """);

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task UserdataToStringUsesManagedToString()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return tostring(vec.new(3, 4))");

        Assert.True(result.Ok);
        Assert.Equal("Vec2(3, 4)", result.Unwrap());
    }

    [Fact]
    public async Task UserdataGcReleasesManagedHandle()
    {
        GcProbe.FinalizedCount = 0;
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("local value = gc_probe.new(); value = nil; collectgarbage('collect')");

        Assert.True(result.Ok);

        for (var i = 0; i < 10 && GcProbe.FinalizedCount == 0; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.True(GcProbe.FinalizedCount > 0);
    }

    [Fact]
    public async Task UserdataCanBePassedAsParameter()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<double>("return vec.dot(vec.new(3, 4), vec.new(1, 2))");

        Assert.True(result.Ok);
        Assert.Equal(11, result.Unwrap(), precision: 12);
    }

    [Fact]
    public async Task MultipleUserdataTypesCanBeRegisteredTogether()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<double>("local v = vec.new(3, 4); local t = tag.new('pop'); return v:length() + tag.size(t)");

        Assert.True(result.Ok);
        Assert.Equal(8, result.Unwrap(), precision: 12);
    }

    [Fact]
    public async Task InvalidUserdataTypeReturnsLuaError()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return vec.dot(tag.new('bad'), vec.new(1, 2))");

        Assert.False(result.Ok);
        var error = Assert.IsType<NativeTypeException>(result.Error);
        Assert.Contains("PopLua.Userdata.vec2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserdataWritablePropertiesAndFieldsCanBeSetFromLua()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<long>("local c = ucounter.new(); c.value = 41; c.label = 'ok'; return c.value + ucounter.label_len(c)");

        Assert.True(result.Ok);
        Assert.Equal(43, result.Unwrap());
    }

    [Fact]
    public async Task NullUserdataReturnPushesNil()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<bool>("return vec.maybe(false) == nil");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task LuaIgnoreMembersAreNotVisibleFromLua()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<bool>("local v = ignored_userdata.new(); return v.hidden == nil and v.secret == nil and v:visible() == 3");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task UserdataMethodFailureReturnsResultFailure()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("vec.new(1, 2):fail()");

        Assert.False(result.Ok);
        var error = Assert.IsType<ScriptException>(result.Error);
        Assert.Contains("vec failure", error.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public async Task ManagedCallbackFailureInsideLuaPCallStillFailsSession()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("caught = pcall(function() vec.new(1, 2):fail() end)");

        Assert.False(result.Ok);
        Assert.Contains("vec failure", result.Error!.Message, StringComparison.Ordinal);

        var pcallResult = await session.Run<bool>("return caught");
        Assert.True(pcallResult.Ok);
        Assert.True(pcallResult.Unwrap());
    }

    [Fact]
    public async Task ManagedCallbackFailureDoesNotPoisonNextRun()
    {
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var failed = await session.Run("vec.new(1, 2):fail()");
        var next = await session.Run<long>("return 42");

        Assert.False(failed.Ok);
        Assert.True(next.Ok);
        Assert.Equal(42, next.Unwrap());
    }

    [Fact]
    public async Task UserdataCallbackCannotReenterSameSession()
    {
        var bridge = new NestedLuaBridge();
        var services = Services.Create().Add(bridge);
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        bridge.Session = session;

        var result = await session.Run("function host_value() return 37 end; return nested.call_host()");

        Assert.False(result.Ok);
        Assert.Contains("active execution", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandledNestedSameSessionCallCannotBypassReentryGuard()
    {
        var bridge = new NestedLuaBridge();
        var services = Services.Create().Add(bridge);
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        bridge.Session = session;

        var result = await session.Run("function host_fail() vec.new(1, 2):fail() end; return nested.call_fail_handled()");

        Assert.False(result.Ok);
        Assert.Contains("active execution", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnhandledNestedSameSessionCallFailsOuterCall()
    {
        var bridge = new NestedLuaBridge();
        var services = Services.Create().Add(bridge);
        var lua = Engine.Create(b => b.Modules<VecModule, TagModule, UserCounterModule, GcProbeModule, NestedModule, IgnoredUserdataModule>());

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        bridge.Session = session;

        var result = await session.Run("function host_fail() vec.new(1, 2):fail() end; return nested.call_fail_unhandled()");

        Assert.False(result.Ok);
        Assert.Contains("active execution", result.Error!.Message, StringComparison.Ordinal);
    }
}

[Module("vec")]
public partial class VecModule
{
    [Fn("new")]
    public static Vec2 New(double x, double y) => new(x, y);

    [Fn("dot")]
    public static double Dot(Vec2 left, Vec2 right) => left.X * right.X + left.Y * right.Y;

    [Fn("maybe")]
    public static Vec2 Maybe(bool create) => create ? new Vec2(1, 2) : null!;
}

[Userdata("vec2")]
public partial class Vec2(double x, double y)
{
    [Prop("x", ReadOnly = true)]
    public double X { get; } = x;

    [Prop("y", ReadOnly = true)]
    public double Y { get; } = y;

    [Fn("length")]
    public double Length() => Math.Sqrt(X * X + Y * Y);

    [Fn("fail")]
    public void Fail() => throw new InvalidOperationException("vec failure");

    public override string ToString() => $"Vec2({X}, {Y})";

    public override bool Equals(object? obj)
        => obj is Vec2 other && X.Equals(other.X) && Y.Equals(other.Y);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static Vec2 operator +(Vec2 left, Vec2 right)
        => new(left.X + right.X, left.Y + right.Y);

    public static Vec2 operator -(Vec2 left, Vec2 right)
        => new(left.X - right.X, left.Y - right.Y);

    public static Vec2 operator *(Vec2 left, Vec2 right)
        => new(left.X * right.X, left.Y * right.Y);

    public static Vec2 operator /(Vec2 left, Vec2 right)
        => new(left.X / right.X, left.Y / right.Y);

    public static Vec2 operator -(Vec2 value)
        => new(-value.X, -value.Y);

    public static bool operator ==(Vec2 left, Vec2 right)
        => left.Equals(right);

    public static bool operator !=(Vec2 left, Vec2 right)
        => !left.Equals(right);

    public static bool operator <(Vec2 left, Vec2 right)
        => left.Length() < right.Length();

    public static bool operator >(Vec2 left, Vec2 right)
        => left.Length() > right.Length();

    public static bool operator <=(Vec2 left, Vec2 right)
        => left.Length() <= right.Length();

    public static bool operator >=(Vec2 left, Vec2 right)
        => left.Length() >= right.Length();
}

[Module("tag")]
public partial class TagModule
{
    [Fn("new")]
    public static NameTag New(string value) => new(value);

    [Fn("size")]
    public static long Size(NameTag tag) => tag.Value.Length;
}

[Userdata("name_tag")]
public partial class NameTag(string value)
{
    [Prop("value", ReadOnly = true)]
    public string Value { get; } = value;
}

[Module("ucounter")]
public partial class UserCounterModule
{
    [Fn("new")]
    public static UserCounter New() => new();

    [Fn("label_len")]
    public static long LabelLength(UserCounter counter) => counter.Label.Length;
}

[Userdata("user_counter", Setters = true)]
public partial class UserCounter
{
    [Prop("value")]
    public long Value { get; set; }

    [Prop("label")]
    public string Label = "";
}

[Module("gc_probe")]
public partial class GcProbeModule
{
    [Fn("new")]
    public static GcProbe New() => new();
}

[Userdata("gc_probe")]
public partial class GcProbe
{
    public static int FinalizedCount;

    ~GcProbe()
    {
        Interlocked.Increment(ref FinalizedCount);
    }
}

[Module("nested")]
public partial class NestedModule(NestedLuaBridge bridge)
{
    [Fn("call_host")]
    public long CallHost() => bridge.CallHost();

    [Fn("call_fail_handled")]
    public long CallFailHandled() => bridge.CallFailHandled();

    [Fn("call_fail_unhandled")]
    public long CallFailUnhandled() => bridge.CallFailUnhandled();
}

public sealed class NestedLuaBridge
{
    public Session? Session { get; set; }

    public long CallHost()
        => Session!.Call<long>("host_value").AsTask().GetAwaiter().GetResult().Unwrap();

    public long CallFailHandled()
    {
        var result = Session!.Call<long>("host_fail").AsTask().GetAwaiter().GetResult();
        return result.Ok ? result.Unwrap() : 7;
    }

    public long CallFailUnhandled()
        => Session!.Call<long>("host_fail").AsTask().GetAwaiter().GetResult().Unwrap();
}

[Module("ignored_userdata")]
public partial class IgnoredUserdataModule
{
    [Fn("new")]
    public static IgnoredUserdata New() => new();
}

[Userdata("ignored_userdata")]
public partial class IgnoredUserdata
{
    [Ignore]
    [Fn("hidden")]
    public long Hidden() => 1;

    [Ignore]
    [Prop("secret")]
    public long Secret { get; set; } = 2;

    [Fn("visible")]
    public long Visible() => 3;
}

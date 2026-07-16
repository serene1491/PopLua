using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Module<VecModule>());

var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
await using var session = lua.Session(sandbox);

var result = await session.Run<string>("""
    local a = vec.new(3, 4)
    local b = vec.new(1, 2)
    local c = a + b

    -- Userdata instance methods use Lua ':' so PopLua receives the C# object.
    return tostring(c) .. " length=" .. c:length() .. " x=" .. c.x .. " y=" .. c.y
    """);

Console.WriteLine(result.Unwrap());

[Module("vec")]
public partial class VecModule
{
    [Fn("new")]
    public static Vec2 New(double x, double y) => new(x, y);
}

[Userdata("vec2")]
public partial class Vec2(double x, double y)
{
    [Prop("x", ReadOnly = true)] public double X { get; } = x;
    [Prop("y", ReadOnly = true)] public double Y { get; } = y;

    [Fn("length")]
    public double Length() => Math.Sqrt(X * X + Y * Y);

    public override string ToString() => $"Vec2({X}, {Y})";

    public static Vec2 operator +(Vec2 a, Vec2 b)
        => new(a.X + b.X, a.Y + b.Y);
}

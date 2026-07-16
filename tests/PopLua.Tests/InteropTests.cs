namespace PopLua.Tests;

public sealed class InteropTests
{
    [Fact]
    public void LongRoundTripsThroughStateStack()
    {
        using var lua = State.Create();
        var stack = lua.Stack;

        stack.PushInteger(42);

        Assert.Equal(42, stack.ReadInteger(-1));
    }

    [Fact]
    public void StringRoundTripsThroughStateStack()
    {
        using var lua = State.Create();
        var stack = lua.Stack;

        stack.PushString("hello");

        Assert.Equal("hello", stack.ReadString(-1));
    }

    [Fact]
    public void Utf8StringCanBeReadWithoutManagedStringAllocation()
    {
        using var lua = State.Create();
        var stack = lua.Stack;

        stack.PushStringUtf8("olá"u8);

        Assert.True(stack.ReadStringUtf8(-1).SequenceEqual("olá"u8));
    }

    [Fact]
    public void ValueNumberRoundTripsThroughStateStack()
    {
        using var lua = State.Create();
        var stack = lua.Stack;

        stack.PushValue(Value.From(3.14));
        var value = stack.ReadValue(-1);

        Assert.Equal(ValueKind.Number, value.Kind);
        Assert.True(value.TryNumber(out var number));
        Assert.Equal(3.14, number);
    }

    [Fact]
    public void ValueNilRoundTripsThroughStateStack()
    {
        using var lua = State.Create();
        var stack = lua.Stack;

        stack.PushNil();

        Assert.True(stack.ReadValue(-1).IsNil);
    }
}

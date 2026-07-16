namespace PopLua.Tests;

public sealed class ValueTests
{
    [Fact]
    public void IntValueCanBeRead()
    {
        var value = Value.From(42L);

        Assert.True(value.TryInt(out var actual));
        Assert.Equal(42, actual);
    }

    [Fact]
    public void NilReportsIsNil()
        => Assert.True(Value.Nil.IsNil);

    [Fact]
    public void StringValueHasStringKind()
        => Assert.Equal(ValueKind.String, Value.From("hello").Kind);

    [Fact]
    public void ErrorResultThrowsOnUnwrap()
    {
        var result = Result<long>.Failure(new ScriptException("boom"));

        Assert.Throws<ScriptException>(() => result.Unwrap());
    }

    [Fact]
    public void ErrorResultPreservesSpecificErrorTypeOnUnwrap()
    {
        var result = Result<long>.Failure(new QuotaException(QuotaKind.Memory));

        var error = Assert.Throws<QuotaException>(() => result.Unwrap());
        Assert.Equal(QuotaKind.Memory, error.Kind);
    }

    [Fact]
    public void SuccessResultReturnsValueOnUnwrap()
    {
        var result = Result<long>.Success(42);

        Assert.Equal(42, result.Unwrap());
    }

    [Fact]
    public void SandboxExceptionMessageContainsCapability()
    {
        var error = new SandboxException("fs.read");

        Assert.Contains("fs.read", error.Message, StringComparison.Ordinal);
    }
}

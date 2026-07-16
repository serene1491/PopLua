namespace PopLua.Tests;

public sealed class GeneratedModuleRuntimeTests
{
    [Fact]
    public async Task GeneratedModuleRegistersAndRuns()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<long>("return mathx.add(20, 22)");

        Assert.True(result.Ok);
        Assert.Equal(42, result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleUsesSnakeCaseDefaultName()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return mathx.get_user_name('Serene')");

        Assert.True(result.Ok);
        Assert.Equal("user:Serene", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleSupportsVariadicValues()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<long>("return mathx.sum(1, 2, 3, 4)");

        Assert.True(result.Ok);
        Assert.Equal(10, result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleSupportsMixedPrimitiveVarargs()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return mathx.kinds(nil, true, 7, 2.5, 'hi')");

        Assert.True(result.Ok);
        Assert.Equal("Nil,Bool,Int,Number,String", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleSupportsOpaqueVarargKinds()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule, VecModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return mathx.kinds({}, function() end, vec.new(1, 2))");

        Assert.True(result.Ok);
        Assert.Equal("Table,Function,Userdata", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleDoesNotPushOpaqueValueArrayReturns()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule, VecModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return mathx.echo_all(vec.new(1, 2))");

        Assert.False(result.Ok);
        Assert.Contains("pushable Lua value", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedModuleSupportsOrdinaryValueParameter()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return mathx.inspect('hello')");

        Assert.True(result.Ok);
        Assert.Equal("String", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleCopiesMarkedResultsIntoFreshTables()
    {
        var lua = Engine.Create(builder => builder.Module<GeneratedMathModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>(
            "local r = mathx.operation(); return tostring(r.ok) .. ':' .. r.details[1].name .. ':' .. r.tags[2]");

        Assert.True(result.Ok);
        Assert.Equal("true:primary:stable", result.Unwrap());
    }

    [Fact]
    public async Task ActiveTimeQuotaCountsSynchronousBindingWorkAtCheckpoint()
    {
        var lua = Engine.Create(b => b.Module<SlowModule>());
        var sandbox = Sandbox.Build(b => b.Quota(
            activeTime: TimeSpan.FromMilliseconds(5),
            wallTime: TimeSpan.FromSeconds(1)));

        await using var session = lua.Session(sandbox);
        var result = await session.Run("return slow.block()");

        var error = Assert.IsType<QuotaException>(result.Error);
        Assert.False(result.Ok);
        Assert.Equal(QuotaKind.ActiveTime, error.Kind);
    }

    [Fact]
    public async Task GeneratedModuleRespectsCapability()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Untrusted);
        var result = await session.Run<bool>("return secret == nil");

        Assert.True(result.Ok);
        Assert.True(result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleRegistersConstants()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<double>("return mathx.PI");

        Assert.True(result.Ok);
        Assert.Equal(Math.PI, result.Unwrap(), precision: 12);
    }

    [Fact]
    public async Task GeneratedModuleRegistersStaticProperties()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return mathx.version");

        Assert.True(result.Ok);
        Assert.Equal("1.0", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleComputedPropertyReadsScriptContextServices()
    {
        var lua = Engine.Create(b => b.Module<ContextModule>());
        var services = Services.Create().Add(new RequestState("Serene", "1491"));

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<string>("return ctx.author.username .. ':' .. ctx.guild.name");

        Assert.True(result.Ok);
        Assert.Equal("Serene:1491", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleComputedPropertyIsPerSession()
    {
        var lua = Engine.Create(b => b.Module<ContextModule>());
        var serene = Services.Create().Add(new RequestState("Serene", null));
        var grace = Services.Create().Add(new RequestState("Grace", "Compiler"));

        await using var first = lua.Session(Sandbox.Trusted, services: serene);
        await using var second = lua.Session(Sandbox.Trusted, services: grace);

        var firstResult = await first.Run<string>("return ctx.author.username .. ':' .. tostring(ctx.guild == nil)");
        var secondResult = await second.Run<string>("return ctx.author.username .. ':' .. ctx.guild.name");

        Assert.True(firstResult.Ok);
        Assert.True(secondResult.Ok);
        Assert.Equal("Serene:true", firstResult.Unwrap());
        Assert.Equal("Grace:Compiler", secondResult.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleReceivesCurrentScriptContext()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());

        await using var session = lua.Session(Sandbox.Trusted, Identity.Create("user-123", "Serene"));
        var result = await session.Run<string>("return mathx.identity()");

        Assert.True(result.Ok);
        Assert.Equal("Serene", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedInstanceModuleResolvesServices()
    {
        var lua = Engine.Create(b => b.Modules<GeneratedMathModule, SecretModule, CounterModule>());
        var services = Services.Create().Add<ICounter>(new Counter());

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<long>("return counter.inc('hits', 3)");

        Assert.True(result.Ok);
        Assert.Equal(3, result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModuleReadsStructuredDescriptorTable()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("""
            return ui.select("choice", {
                placeholder = "Choose",
                options = {
                    { label = "Option A", value = "a" },
                    { label = "Option B", value = "b" }
                }
            })
            """);

        Assert.True(result.Ok);
        Assert.Equal("choice:Choose:Option A=a,Option B=b", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedDescriptorMissingFieldsUseClrInitializers()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return ui.select('choice', {})");

        Assert.True(result.Ok);
        Assert.Equal("choice::", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedDescriptorReadsExplicitFieldName()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return ui.explicit_field({ fieldName = 'value' })");

        Assert.True(result.Ok);
        Assert.Equal("value", result.Unwrap());
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("nil", "nil")]
    public async Task GeneratedDescriptorReadsNullableScalar(string luaValue, string expected)
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>($"return ui.optional_flag({{ enabled = {luaValue} }})");

        Assert.True(result.Ok);
        Assert.Equal(expected, result.Unwrap());
    }

    [Fact]
    public async Task GeneratedModulesRegisterTransitiveUserdataReturns()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return ui.parent():child().name");

        Assert.True(result.Ok);
        Assert.Equal("nested", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedDescriptorSupportsEmptyDescriptorLists()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return ui.select('choice', { options = {} })");

        Assert.True(result.Ok);
        Assert.Equal("choice::", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedDescriptorInvalidListElementFailsClearly()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return ui.select('choice', { options = { true } })");

        Assert.False(result.Ok);
        Assert.Contains("invalid descriptor field", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("select_descriptor.options[1]", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedDescriptorRejectsNamedFieldsInDescriptorLists()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return ui.select('choice', { options = { first = { label = 'A', value = 'a' } } })");

        Assert.False(result.Ok);
        Assert.Contains("select_descriptor.options must be a dense array", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedDescriptorWrongFieldTypeFailsClearly()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return ui.select('choice', { placeholder = true })");

        Assert.False(result.Ok);
        Assert.Contains("select_descriptor.placeholder", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("string", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedDescriptorMissingRequiredNestedFieldFailsClearly()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return ui.select('choice', { options = { { value = 'a' } } })");

        Assert.False(result.Ok);
        Assert.Contains("select_descriptor.options[1].label", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedDescriptorWrongNestedFieldTypeIncludesPath()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return ui.select('choice', { options = { { label = true, value = 'a' } } })");

        Assert.False(result.Ok);
        Assert.Contains("select_descriptor.options[1].label", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedDescriptorRejectsUnknownFields()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return ui.select('choice', { placeholder = 'Choose', unknown = true })");

        Assert.False(result.Ok);
        Assert.Contains("unknown descriptor field", result.Error!.Message, StringComparison.Ordinal);
        Assert.Contains("select_descriptor.unknown", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedDescriptorReadsDenseStringLists()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return ui.tags({ values = { 'one', 'two' } })");

        Assert.True(result.Ok);
        Assert.Equal("one,two", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedDescriptorRejectsNamedStringListFields()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return ui.tags({ values = { one = 'two' } })");

        Assert.False(result.Ok);
        Assert.Contains("string_list_descriptor.values must be a dense array", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedDescriptorStringListErrorsIncludeTheIndex()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run("return ui.tags({ values = { 'one', true } })");

        Assert.False(result.Ok);
        Assert.Contains("string_list_descriptor.values[2]", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratedDescriptorCanContainUserdata()
    {
        var lua = Engine.Create(b => b.Modules<DescriptorModule, ContextModule>());
        var services = Services.Create().Add(new RequestState("Serene", null));

        await using var session = lua.Session(Sandbox.Trusted, services: services);
        var result = await session.Run<string>("return ui.describe_user({ user = ctx.author, note = 'ready' })");

        Assert.True(result.Ok);
        Assert.Equal("Serene:ready", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedDescriptorNullableUserdataFieldUsesInitializerWhenMissing()
    {
        var lua = Engine.Create(b => b.Modules<DescriptorModule, ContextModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return ui.maybe_user({ note = 'none' })");

        Assert.True(result.Ok);
        Assert.Equal("none:nil", result.Unwrap());
    }

    [Fact]
    public async Task GeneratedDescriptorCanContainValue()
    {
        var lua = Engine.Create(b => b.Module<DescriptorModule>());

        await using var session = lua.Session(Sandbox.Trusted);
        var result = await session.Run<string>("return ui.inspect_value({ value = {} })");

        Assert.True(result.Ok);
        Assert.Equal("Table", result.Unwrap());
    }
}

[Module("mathx")]
public partial class GeneratedMathModule
{
    [Const("PI")]
    public const double Pi = Math.PI;

    [Prop("version", ReadOnly = true)]
    public static string Version => "1.0";

    [Fn("add")]
    public static long Add(long a, long b) => a + b;

    [Fn]
    public static string GetUserName(string name) => "user:" + name;

    [Fn("sum")]
    public static long Sum(Value[] values)
    {
        long total = 0;

        foreach (var value in values)
            total += value.Int();

        return total;
    }

    [Fn("kinds")]
    public static string Kinds(Value[] values)
        => string.Join(",", values.Select(value => value.Kind.ToString()));

    [Fn("echo_all")]
    public static Value[] EchoAll(Value[] values) => values;

    [Fn("inspect")]
    public static string Inspect(Value value) => value.Kind.ToString();

    [Fn("operation")]
    public static OperationResult Operation()
        => new(
            true,
            [new OperationDetail("primary")],
            ["generated", "stable"]);

    [Fn("identity")]
    public static string Identity([Context] ScriptContext ctx)
        => ctx.Identity.Name ?? ctx.Identity.Id;
}

/// <summary>
/// Copy-based result returned to Lua as a fresh table.
/// </summary>
[Table]
public sealed record OperationResult(
    bool Ok,
    IReadOnlyList<OperationDetail> Details,
    IReadOnlyList<string> Tags);

/// <summary>
/// Nested copy-based result returned inside an operation.
/// </summary>
[Table]
public sealed record OperationDetail(string Name);

[Module("secret", Cap = Caps.FileRead)]
public partial class SecretModule
{
    [Fn("value")]
    public static long Value() => 42;
}

[Module("counter")]
public partial class CounterModule(ICounter counter)
{
    [Fn("inc")]
    public long Inc(string key, long by)
        => counter.Inc(key, by);
}

public interface ICounter
{
    long Inc(string key, long by);
}

public sealed class Counter : ICounter
{
    private readonly Dictionary<string, long> _values = [];

    public long Inc(string key, long by)
    {
        _values.TryGetValue(key, out var current);
        current += by;
        _values[key] = current;
        return current;
    }
}

public sealed record RequestState(string Author, string? Guild);

[Module("slow")]
public partial class SlowModule
{
    [Fn("block")]
    public static long Block()
    {
        Thread.Sleep(TimeSpan.FromMilliseconds(50));
        return 1;
    }
}

[Module("ctx")]
public partial class ContextModule
{
    /// <summary>Current author for this script execution.</summary>
    [Prop("author")]
    public static ContextUser Author([Context] ScriptContext context)
        => new(((RequestState)context.Services.GetService(typeof(RequestState))!).Author);

    /// <summary>Current guild for this script execution, when any.</summary>
    [Prop("guild")]
    public static ContextGuild? Guild([Context] ScriptContext context)
    {
        var state = (RequestState)context.Services.GetService(typeof(RequestState))!;
        return state.Guild is null ? null : new ContextGuild(state.Guild);
    }
}

[Userdata("context_user")]
public partial class ContextUser(string username)
{
    [Prop("username", ReadOnly = true)]
    public string Username { get; } = username;
}

[Userdata("context_guild")]
public partial class ContextGuild(string name)
{
    [Prop("name", ReadOnly = true)]
    public string Name { get; } = name;
}

[Module("ui")]
public partial class DescriptorModule
{
    [Fn("select")]
    public static string Select(string id, SelectDescriptor descriptor)
        => id + ":" + descriptor.Placeholder + ":" +
           string.Join(",", descriptor.Options.Select(option => option.Label + "=" + option.Value));

    [Fn("describe_user")]
    public static string DescribeUser(UserDescriptor descriptor)
        => descriptor.User.Username + ":" + descriptor.Note;

    [Fn("maybe_user")]
    public static string MaybeUser(OptionalUserDescriptor descriptor)
        => descriptor.Note + ":" + (descriptor.User is null ? "nil" : descriptor.User.Username);

    [Fn("inspect_value")]
    public static string InspectValue(ValueDescriptor descriptor)
        => descriptor.Value.Kind.ToString();

    [Fn("tags")]
    public static string Tags(StringListDescriptor descriptor)
        => string.Join(",", descriptor.Values);

    [Fn("explicit_field")]
    public static string ExplicitField(ExplicitFieldDescriptor descriptor)
        => descriptor.Field;

    [Fn("optional_flag")]
    public static string OptionalFlag(OptionalFlagDescriptor descriptor)
        => descriptor.Enabled?.ToString().ToLowerInvariant() ?? "nil";

    [Fn("parent")]
    public static ParentUserdata Parent() => new();
}

public sealed class SelectDescriptor
{
    public string? Placeholder { get; init; }
    public IReadOnlyList<SelectOptionDescriptor> Options { get; init; } = [];
}

public sealed class ExplicitFieldDescriptor
{
    [Field("fieldName")]
    public string Field { get; init; } = "";
}

public sealed class OptionalFlagDescriptor
{
    public bool? Enabled { get; init; }
}

[Userdata("ParentUserdata")]
public partial class ParentUserdata
{
    [Fn("child")]
    public ChildUserdata Child() => new();
}

[Userdata("ChildUserdata")]
public partial class ChildUserdata
{
    [Prop("name", ReadOnly = true)]
    public string Name { get; } = "nested";
}

public sealed class SelectOptionDescriptor
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}

public sealed class UserDescriptor
{
    public required ContextUser User { get; init; }
    public string? Note { get; init; }
}

public sealed class OptionalUserDescriptor
{
    public ContextUser? User { get; init; }
    public string? Note { get; init; }
}

public sealed class ValueDescriptor
{
    public Value Value { get; init; }
}

public sealed class StringListDescriptor
{
    public IReadOnlyList<string> Values { get; init; } = [];
}

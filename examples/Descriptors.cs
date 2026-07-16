using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Module<UiModule>());

await using var session = lua.Session(Sandbox.Untrusted);

var result = await session.Run<string>("""
    return ui.select("choice", {
        placeholder = "Choose an option",
        tags = { "compact", "searchable" },
        options = {
            { label = "Option A", value = "a" },
            { label = "Option B", value = "b" }
        }
    })
    """);

Console.WriteLine(result.Unwrap());

[Module("ui")]
public partial class UiModule
{
    [Fn("select")]
    public static string Select(string id, SelectDescriptor descriptor)
        => id + ": " + descriptor.Placeholder + " -> " +
           string.Join(", ", descriptor.Options.Select(option => option.Label + "=" + option.Value)) +
           " [" + string.Join(", ", descriptor.Tags) + "]";
}

public sealed class SelectDescriptor
{
    public string? Placeholder { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<SelectOptionDescriptor> Options { get; init; } = [];
}

public sealed class SelectOptionDescriptor
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}

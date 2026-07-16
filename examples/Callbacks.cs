using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var callbacks = new ButtonCallbacks();
var services = Services.Create().Add(callbacks);
var lua = Engine.Create(b => b.Module<ButtonModule>());

await using var session = lua.Session(Sandbox.Untrusted, services: services);

(await session.Run("""
    button.on_click(function(label)
        return "clicked: " .. label
    end)
    """)).ThrowIfError();

var clicked = callbacks.Clicked ?? throw new InvalidOperationException("Callback was not registered.");
var result = await clicked.Call<string>(Value.From("Save"));

Console.WriteLine(result.Unwrap());
await clicked.DisposeAsync();

[Module("button")]
public partial class ButtonModule(ButtonCallbacks callbacks)
{
    [Fn("on_click")]
    public void OnClick(FunctionRef callback) => callbacks.Clicked = callback;
}

public sealed class ButtonCallbacks
{
    public FunctionRef? Clicked { get; set; }
}

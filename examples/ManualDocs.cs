using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;
using PopLua.Generated;

var output = Path.Combine(AppContext.BaseDirectory, "poplua-api");
Directory.CreateDirectory(output);

File.WriteAllText(Path.Combine(output, "poplua.api.json"), PopLuaApiManifestProvider.Json);
File.WriteAllText(Path.Combine(output, "poplua.d.lua"), PopLuaLuaLsDefinitionProvider.Lua);
File.WriteAllText(Path.Combine(output, "poplua-api.md"), PopDocumentationProvider.Markdown);

Console.WriteLine(output);

/// <summary>Quest functions exposed to Lua scripts.</summary>
[Module("quests", Cap = "quests.read")]
public partial class QuestModule
{
    /// <summary>Gets the current quest title.</summary>
    /// <remarks>Manual generation writes this documentation through the generated provider.</remarks>
    /// <returns>The current quest title.</returns>
    [Fn("current")]
    public static string Current() => "Find the moon gate";
}

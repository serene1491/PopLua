using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace SplitHost.App.Scripting;

internal sealed class ScriptCatalog
{
    private readonly string _main;
    private readonly Dictionary<string, string> _modules;

    private ScriptCatalog(string main, Dictionary<string, string> modules)
    {
        _main = main;
        _modules = modules;
    }

    public static ScriptCatalog Load(string directory)
    {
        var main = File.ReadAllText(Path.Combine(directory, "main.lua"));
        var modules = Directory
            .EnumerateFiles(directory, "*.lua")
            .Where(path => !Path.GetFileName(path).Equals("main.lua", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                File.ReadAllText,
                StringComparer.Ordinal);

        return new ScriptCatalog(main, modules);
    }

    public Chunk Entrypoint(string name)
        => Chunk.Code(_main, name);

    public Chunk? ResolveModule(ScriptContext context, string name)
        => _modules.TryGetValue(name, out var source)
            ? Chunk.Code(source, $"{context.Identity.Id}:{name}.lua")
            : null;
}

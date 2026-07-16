using System.Collections.Generic;

namespace PopLua.Generators.Manifest;

internal sealed class ModuleModel
{
    public ModuleModel(
        string name,
        string? capability,
        string? csName = null,
        SourceSymbol? source = null,
        Documentation? documentation = null)
    {
        Id = Ids.Module(name);
        Name = name;
        Capability = capability;
        CsName = csName;
        Source = source;
        Documentation = documentation ?? Documentation.Empty;
    }

    public string Id { get; }
    public string Name { get; }
    public string? CsName { get; }
    public string? Capability { get; }
    public SourceSymbol? Source { get; }
    public Documentation Documentation { get; }
    public List<FunctionModel> Functions { get; } = [];
    public List<ValueModel> Values { get; } = [];
}

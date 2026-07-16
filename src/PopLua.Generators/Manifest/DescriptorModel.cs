using System.Collections.Generic;

namespace PopLua.Generators.Manifest;

internal sealed class DescriptorModel
{
    public DescriptorModel(
        string name,
        string? csName = null,
        SourceSymbol? source = null,
        Documentation? documentation = null)
    {
        Id = Ids.Descriptor(name);
        Name = name;
        CsName = csName;
        Source = source;
        Documentation = documentation ?? Documentation.Empty;
    }

    public string Id { get; }
    public string Name { get; }
    public string? CsName { get; }
    public SourceSymbol? Source { get; }
    public Documentation Documentation { get; }
    public List<ValueModel> Fields { get; } = [];
}

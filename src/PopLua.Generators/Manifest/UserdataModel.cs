using System.Collections.Generic;

namespace PopLua.Generators.Manifest;

internal sealed class UserdataModel
{
    public UserdataModel(
        string name,
        string? csName = null,
        bool setters = false,
        bool toString = true,
        bool gc = true,
        SourceSymbol? source = null,
        Documentation? documentation = null)
    {
        Id = Ids.Userdata(name);
        Name = name;
        CsName = csName;
        Setters = setters;
        EmitsToString = toString;
        Gc = gc;
        Source = source;
        Documentation = documentation ?? Documentation.Empty;
    }

    public string Id { get; }
    public string Name { get; }
    public string? CsName { get; }
    public bool Setters { get; }
    public bool EmitsToString { get; }
    public bool Gc { get; }
    public SourceSymbol? Source { get; }
    public Documentation Documentation { get; }
    public List<FunctionModel> Methods { get; } = [];
    public List<ValueModel> Properties { get; } = [];
    public List<OperatorModel> Operators { get; } = [];
}

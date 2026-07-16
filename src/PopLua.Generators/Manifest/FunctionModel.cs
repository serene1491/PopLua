using System.Collections.Generic;

namespace PopLua.Generators.Manifest;

internal sealed class FunctionModel
{
    public FunctionModel(
        string id,
        string name,
        bool isAsync,
        string? csName = null,
        bool isStatic = true,
        bool countWait = false,
        string? capability = null,
        int overload = 0,
        string kind = "function",
        SourceSymbol? source = null,
        Documentation? documentation = null)
    {
        Id = id;
        Name = name;
        CsName = csName;
        IsAsync = isAsync;
        IsStatic = isStatic;
        CountWait = countWait;
        Capability = capability;
        Overload = overload;
        Kind = kind;
        Source = source;
        Documentation = documentation ?? Documentation.Empty;
    }

    public string Id { get; }
    public string Name { get; }
    public string? CsName { get; }
    public string Kind { get; }
    public bool IsAsync { get; }
    public bool IsStatic { get; }
    public bool CountWait { get; }
    public string? Capability { get; }
    public int Overload { get; }
    public SourceSymbol? Source { get; }
    public Documentation Documentation { get; }
    public List<ParameterModel> Parameters { get; } = [];
    public List<ReturnModel> Returns { get; } = [];
}

using System.Collections.Generic;

namespace PopLua.Generators.Manifest;

internal sealed class OperatorModel
{
    public OperatorModel(
        string id,
        string metamethod,
        ReturnModel returns,
        string? csName = null,
        SourceSymbol? source = null,
        Documentation? documentation = null)
    {
        Id = id;
        Metamethod = metamethod;
        CsName = csName;
        Return = returns;
        Source = source;
        Documentation = documentation ?? Documentation.Empty;
    }

    public string Id { get; }
    public string Metamethod { get; }
    public string? CsName { get; }
    public ReturnModel Return { get; }
    public SourceSymbol? Source { get; }
    public Documentation Documentation { get; }
    public List<ParameterModel> Parameters { get; } = [];
}

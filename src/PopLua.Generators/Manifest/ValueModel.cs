namespace PopLua.Generators.Manifest;

internal sealed class ValueModel
{
    public ValueModel(
        string id,
        string name,
        ApiType type,
        bool isWritable,
        string kind = "property",
        string? csName = null,
        SourceSymbol? source = null,
        Documentation? documentation = null)
    {
        Id = id;
        Name = name;
        CsName = csName;
        Type = type;
        IsWritable = isWritable;
        Kind = kind;
        Source = source;
        Documentation = documentation ?? Documentation.Empty;
    }

    public string Id { get; }
    public string Name { get; }
    public string? CsName { get; }
    public string Kind { get; }
    public ApiType Type { get; }
    public bool IsWritable { get; }
    public bool IsReadOnly => !IsWritable;
    public SourceSymbol? Source { get; }
    public Documentation Documentation { get; }
}

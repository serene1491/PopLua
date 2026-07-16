namespace PopLua.Generators.Manifest;

internal sealed class ParameterModel
{
    public ParameterModel(
        string name,
        ApiType type,
        bool isContext = false,
        bool isVariadic = false,
        string? documentation = null)
    {
        Name = name;
        Type = type;
        IsContext = isContext;
        IsVariadic = isVariadic;
        Documentation = documentation;
    }

    public string Name { get; }
    public ApiType Type { get; }
    public bool IsContext { get; }
    public bool IsVariadic { get; }
    public string? Documentation { get; }
}

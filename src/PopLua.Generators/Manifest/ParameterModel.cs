namespace PopLua.Generators.Manifest;

internal sealed class ParameterModel
{
    public ParameterModel(
        string name,
        ApiType type,
        bool isContext = false,
        bool isVariadic = false,
        bool isOptional = false,
        bool coercesText = false,
        string? documentation = null)
    {
        Name = name;
        Type = type;
        IsContext = isContext;
        IsVariadic = isVariadic;
        IsOptional = isOptional;
        CoercesText = coercesText;
        Documentation = documentation;
    }

    public string Name { get; }
    public ApiType Type { get; }
    public bool IsContext { get; }
    public bool IsVariadic { get; }
    public bool IsOptional { get; }
    public bool CoercesText { get; }
    public string? Documentation { get; }
}

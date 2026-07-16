namespace PopLua.Generators.Manifest;

internal sealed class ReturnModel
{
    public ReturnModel(ApiType type, string? documentation = null)
    {
        Type = type;
        Documentation = documentation;
    }

    public ApiType Type { get; }
    public string? Documentation { get; }
}

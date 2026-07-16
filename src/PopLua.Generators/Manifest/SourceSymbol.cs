namespace PopLua.Generators.Manifest;

internal sealed class SourceSymbol
{
    public SourceSymbol(
        string dotnetDisplayName,
        string metadataName,
        string? containingType,
        string? @namespace,
        string assemblyName)
    {
        DotnetDisplayName = dotnetDisplayName;
        MetadataName = metadataName;
        ContainingType = containingType;
        Namespace = @namespace;
        AssemblyName = assemblyName;
    }

    public string DotnetDisplayName { get; }
    public string MetadataName { get; }
    public string? ContainingType { get; }
    public string? Namespace { get; }
    public string AssemblyName { get; }
}

namespace PopLua.Generators.Manifest;

internal sealed class ExceptionDocumentation
{
    public ExceptionDocumentation(string? cref, string documentation)
    {
        Cref = cref;
        Documentation = documentation;
    }

    public string? Cref { get; }
    public string Documentation { get; }
}

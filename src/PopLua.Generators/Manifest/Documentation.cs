using System.Collections.Generic;

namespace PopLua.Generators.Manifest;

internal sealed class Documentation
{
    public static Documentation Empty { get; } = new(
        null,
        null,
        null,
        new Dictionary<string, string>(),
        [],
        []);

    public Documentation(
        string? summary,
        string? remarks,
        string? returns,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyList<string>? examples = null,
        IReadOnlyList<ExceptionDocumentation>? exceptions = null)
    {
        Summary = summary;
        Remarks = remarks;
        Returns = returns;
        Parameters = parameters;
        Examples = examples ?? [];
        Exceptions = exceptions ?? [];
    }

    public string? Summary { get; }
    public string? Remarks { get; }
    public string? Returns { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
    public IReadOnlyList<string> Examples { get; }
    public IReadOnlyList<ExceptionDocumentation> Exceptions { get; }

    public bool IsEmpty
        => Summary is null
            && Remarks is null
            && Returns is null
            && Parameters.Count == 0
            && Examples.Count == 0
            && Exceptions.Count == 0;
}

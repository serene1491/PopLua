namespace PopLua.Runtime;

/// <summary>
/// Describes one validated controlled-loading request.
/// </summary>
/// <param name="Name">Normalized dot-separated module name.</param>
/// <param name="Caller">Parent module name, or <see langword="null"/> for a root script request.</param>
/// <param name="Depth">One-based controlled-loading depth.</param>
/// <param name="Step">Observable load step.</param>
public readonly record struct Load(string Name, string? Caller, int Depth, Step Step);

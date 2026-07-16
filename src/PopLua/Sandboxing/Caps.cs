namespace PopLua.Sandboxing;

/// <summary>
/// Common capability names used by PopLua and host applications.
/// </summary>
/// <remarks>
/// These constants are names only. They do not perform file, network, process,
/// environment, or debug operations by themselves. Enforcement happens when a
/// generated module declares a capability or host code calls
/// <see cref="Sandbox.Require"/>.
/// </remarks>
public static class Caps
{
    /// <summary>
    /// Capability name for host-provided file reads.
    /// </summary>
    public const string FileRead = "fs.read";

    /// <summary>
    /// Capability name for host-provided file writes.
    /// </summary>
    public const string FileWrite = "fs.write";

    /// <summary>
    /// Capability name for outbound network access.
    /// </summary>
    public const string Net = "net.outbound";

    /// <summary>
    /// Capability name for spawning host processes.
    /// </summary>
    public const string Process = "proc.spawn";

    /// <summary>
    /// Capability name for reading host environment variables.
    /// </summary>
    public const string Env = "env.read";

    /// <summary>
    /// Capability name for debug-oriented functionality.
    /// </summary>
    public const string Debug = "debug";

    /// <summary>
    /// Capability name for profiling-oriented functionality.
    /// </summary>
    public const string Profiling = "profiling";
}

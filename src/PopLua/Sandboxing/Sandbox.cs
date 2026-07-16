namespace PopLua.Sandboxing;

/// <summary>
/// Immutable sandbox policy for Lua execution.
/// </summary>
public sealed class Sandbox
{
    private static readonly StringComparer CapComparer = StringComparer.Ordinal;

    private readonly IReadOnlySet<string> _caps;
    private readonly IReadOnlySet<string> _denied;
    private readonly bool _allowAll;

    internal Sandbox(
        IReadOnlySet<string> caps,
        IReadOnlySet<string> denied,
        bool allowAll,
        long? maxInstructions,
        TimeSpan? maxActiveTime,
        TimeSpan? maxWallTime,
        int? maxCallDepth,
        int hookInterval,
        nuint? maxHeapBytes,
        nuint? gcThresholdBytes,
        Library libs)
    {
        _caps = caps;
        _denied = denied;
        _allowAll = allowAll;
        MaxInstructions = maxInstructions;
        MaxActiveTime = maxActiveTime;
        MaxWallTime = maxWallTime;
        MaxCallDepth = maxCallDepth;
        HookInterval = hookInterval;
        MaxHeapBytes = maxHeapBytes;
        GcThresholdBytes = gcThresholdBytes;
        Libs = libs;
    }

    /// <summary>
    /// Gets the default policy for untrusted scripts.
    /// </summary>
    /// <remarks>
    /// Untrusted sessions start without standard Lua libraries and with default
    /// instruction, active-time, and call-depth quotas. Use this for
    /// user-authored scripts unless the host has a stronger custom policy.
    /// </remarks>
    public static Sandbox Untrusted { get; } = Build(b => b
        .Quota(instructions: 1_000_000, activeTime: TimeSpan.FromSeconds(5), wallTime: TimeSpan.FromSeconds(30), callDepth: 256));

    /// <summary>
    /// Gets the policy for fully trusted internal scripts.
    /// </summary>
    /// <remarks>
    /// Trusted sessions open Lua standard libraries and allow all capabilities.
    /// Do not use this policy for untrusted user-authored scripts.
    /// </remarks>
    public static Sandbox Trusted { get; } = new(
        new HashSet<string>(CapComparer),
        new HashSet<string>(CapComparer),
        allowAll: true,
        maxInstructions: null,
        maxActiveTime: null,
        maxWallTime: null,
        maxCallDepth: null,
        hookInterval: 1000,
        maxHeapBytes: null,
        gcThresholdBytes: null,
        libs: Library.All);

    /// <summary>
    /// Gets the native Lua standard libraries exposed by sessions using this sandbox.
    /// </summary>
    /// <remarks>
    /// This controls only Lua's native standard libraries. PopLua-controlled
    /// <c>require</c> and generated host modules are configured separately.
    /// </remarks>
    public Library Libs { get; }

    internal long? MaxInstructions { get; }
    internal TimeSpan? MaxActiveTime { get; }
    internal TimeSpan? MaxWallTime { get; }
    internal int? MaxCallDepth { get; }
    internal int HookInterval { get; }
    internal nuint? MaxHeapBytes { get; }
    internal nuint? GcThresholdBytes { get; }

    /// <summary>
    /// Builds a sandbox policy.
    /// </summary>
    /// <param name="configure">Callback that configures capabilities and quotas.</param>
    /// <returns>A new immutable sandbox policy.</returns>
    /// <remarks>
    /// `Sandbox.Build` starts from an empty policy, not from
    /// <see cref="Untrusted"/>. Configure quotas explicitly when building a
    /// custom untrusted policy.
    /// </remarks>
    /// <example>
    /// <code>
    /// var sandbox = Sandbox.Build(b => b
    ///     .Allow(Caps.FileRead)
    ///     .Quota(instructions: 100_000, activeTime: TimeSpan.FromSeconds(1), wallTime: TimeSpan.FromSeconds(30)));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static Sandbox Build(Action<Builder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new Builder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Checks whether a capability is allowed.
    /// </summary>
    /// <param name="cap">Exact host-defined capability name.</param>
    /// <returns><see langword="true"/> when the capability is allowed and not denied.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cap"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public bool Has(string cap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cap);
        return !_denied.Contains(cap) && (_allowAll || _caps.Contains(cap));
    }

    /// <summary>
    /// Throws when a capability is not allowed.
    /// </summary>
    /// <param name="cap">Exact host-defined capability name.</param>
    /// <exception cref="SandboxException">Thrown when the capability is not allowed.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cap"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public void Require(string cap)
    {
        if (!Has(cap))
            throw new SandboxException(cap);
    }
}

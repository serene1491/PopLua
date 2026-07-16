namespace PopLua.Sandboxing;

/// <summary>
/// Fluent builder for immutable <see cref="Sandbox"/> policies.
/// </summary>
/// <remarks>
/// The builder starts with no capabilities and no quotas. Calls are order-aware
/// for individual capabilities: <see cref="Allow"/> removes an existing deny for
/// that capability, and <see cref="Deny"/> removes an existing allow. Denied
/// capabilities also override <see cref="AllowAll"/>.
/// </remarks>
public sealed class Builder
{
    private readonly HashSet<string> _allowed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _denied = new(StringComparer.Ordinal);
    private bool _allowAll;
    private long? _maxInstructions;
    private TimeSpan? _maxActiveTime;
    private TimeSpan? _maxWallTime;
    private int? _maxCallDepth;
    private int _hookInterval = 1000;
    private nuint? _maxHeapBytes;
    private nuint? _gcThresholdBytes;
    private Library _libs;

    /// <summary>
    /// Allows a sandbox capability by exact name.
    /// </summary>
    /// <param name="cap">Exact host-defined capability name.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cap"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public Builder Allow(string cap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cap);

        _allowed.Add(cap);
        _denied.Remove(cap);
        return this;
    }

    /// <summary>
    /// Removes or blocks a sandbox capability by exact name.
    /// </summary>
    /// <param name="cap">Exact host-defined capability name.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// A denied capability is blocked even when <see cref="AllowAll"/> is set.
    /// Calling <see cref="Allow"/> for the same capability later removes the deny.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cap"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public Builder Deny(string cap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cap);

        _allowed.Remove(cap);
        _denied.Add(cap);
        return this;
    }

    /// <summary>
    /// Allows all capabilities. Resource quotas still apply when configured.
    /// </summary>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// `AllowAll` affects PopLua capability checks only. It does not grant file,
    /// network, process, or environment access by itself; host APIs must still
    /// enforce those operations.
    /// </remarks>
    public Builder AllowAll()
    {
        _allowAll = true;
        return this;
    }

    /// <summary>
    /// Exposes selected native Lua 5.4 or 5.5 standard libraries.
    /// </summary>
    /// <param name="libraries">Standard libraries to expose.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// This is independent from PopLua capabilities. Use
    /// <see cref="Library.Safe"/> for a conservative user-script
    /// profile, and avoid <see cref="Library.Package"/>,
    /// <see cref="Library.Io"/>, <see cref="Library.Os"/>,
    /// and <see cref="Library.Debug"/> for untrusted scripts unless
    /// the host deliberately accepts those native Lua behaviors.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="libraries"/> contains unsupported flags.</exception>
    public Builder AllowLibs(Library libraries)
    {
        ValidateLibs(libraries);

        _libs |= libraries;
        if ((_libs & Library.FullBase) != 0)
            _libs &= ~Library.SafeBase;

        return this;
    }

    /// <summary>
    /// Exposes PopLua's conservative native Lua standard-library profile.
    /// </summary>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// The safe profile includes selected base functions plus the native
    /// <c>math</c>, <c>string</c>, <c>table</c>, and <c>utf8</c> libraries. It
    /// does not include <c>io</c>, <c>os</c>, <c>package</c>, <c>debug</c>, or
    /// Lua filesystem loading.
    /// </remarks>
    public Builder AllowSafeLibs()
        => AllowLibs(Library.Safe);

    /// <summary>
    /// Configures instruction, active-time, wall-time, and call-depth quotas.
    /// </summary>
    /// <param name="instructions">Maximum approximate Lua VM instructions, or <see langword="null"/> for no instruction limit.</param>
    /// <param name="activeTime">Maximum active execution time, or <see langword="null"/> for no active-time limit.</param>
    /// <param name="wallTime">Maximum total elapsed execution lifetime, or <see langword="null"/> for no wall-time limit.</param>
    /// <param name="callDepth">Maximum nested Lua call depth, or <see langword="null"/> for no call-depth limit.</param>
    /// <param name="hookInterval">Instruction-hook interval used for quota checks.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Active time counts Lua execution and generated host callback work while
    /// it is actively running. Wall time counts total elapsed execution
    /// lifetime, including async waits and suspended coroutine time.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a quota is negative or <paramref name="hookInterval"/> is not positive.</exception>
    public Builder Quota(
        long? instructions = null,
        TimeSpan? activeTime = null,
        TimeSpan? wallTime = null,
        int? callDepth = null,
        int hookInterval = 1000)
    {
        if (instructions < 0)
            throw new ArgumentOutOfRangeException(nameof(instructions));
        if (activeTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(activeTime));
        if (wallTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(wallTime));
        if (callDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(callDepth));
        if (hookInterval <= 0)
            throw new ArgumentOutOfRangeException(nameof(hookInterval));

        _maxInstructions = instructions;
        _maxActiveTime = activeTime;
        _maxWallTime = wallTime;
        _maxCallDepth = callDepth;
        _hookInterval = hookInterval;
        return this;
    }

    /// <summary>
    /// Configures Lua allocator heap limit and GC threshold, in bytes.
    /// </summary>
    /// <param name="heapBytes">Maximum Lua allocator memory in bytes, or <see langword="null"/> for no memory limit.</param>
    /// <param name="gcBytes">Lua allocator usage threshold that requests a Lua garbage collection, or <see langword="null"/> for no threshold.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Memory accounting covers Lua allocator memory, not managed objects
    /// allocated by host callbacks or services.
    /// </remarks>
    public Builder Memory(nuint? heapBytes = null, nuint? gcBytes = null)
    {
        _maxHeapBytes = heapBytes;
        _gcThresholdBytes = gcBytes;
        return this;
    }

    internal Sandbox Build()
    {
        var caps = new HashSet<string>(_allowed, StringComparer.Ordinal);
        caps.ExceptWith(_denied);

        return new Sandbox(
            caps,
            new HashSet<string>(_denied, StringComparer.Ordinal),
            _allowAll,
            _maxInstructions,
            _maxActiveTime,
            _maxWallTime,
            _maxCallDepth,
            _hookInterval,
            _maxHeapBytes,
            _gcThresholdBytes,
            _libs);
    }

    private static void ValidateLibs(Library libraries)
    {
        const Library allowed =
            Library.SafeBase
            | Library.Coroutine
            | Library.Math
            | Library.String
            | Library.Table
            | Library.Utf8
            | Library.FullBase
            | Library.Package
            | Library.Io
            | Library.Os
            | Library.Debug;

        if ((libraries & ~allowed) != 0)
            throw new ArgumentOutOfRangeException(nameof(libraries));
    }
}

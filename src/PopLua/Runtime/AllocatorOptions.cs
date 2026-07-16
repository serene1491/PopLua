namespace PopLua.Runtime;

/// <summary>
/// Lua allocator settings.
/// </summary>
/// <param name="InitialHeapBytes">Initial tracked Lua heap size used by the allocator.</param>
/// <param name="MaxHeapBytes">Maximum Lua allocator memory in bytes; zero means no runtime default limit.</param>
/// <param name="GcThresholdBytes">Lua allocator usage threshold that requests collection; zero means no runtime default threshold.</param>
/// <remarks>
/// These settings apply to Lua allocator memory for new sessions. Managed
/// objects allocated by host callbacks, services, or generated userdata targets
/// are owned by the .NET runtime and are not counted by this allocator.
/// Sandbox memory settings override these runtime defaults for a session.
/// </remarks>
public readonly record struct AllocatorOptions(
    nuint InitialHeapBytes = 0,
    nuint MaxHeapBytes = 0,
    nuint GcThresholdBytes = 0);

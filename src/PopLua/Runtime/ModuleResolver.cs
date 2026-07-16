namespace PopLua.Runtime;

/// <summary>
/// Resolves approved Lua modules for PopLua's controlled <c>require</c> loader.
/// </summary>
/// <param name="context">Current execution context, including identity, sandbox, services, and cancellation.</param>
/// <param name="moduleName">Normalized dot-separated module name requested by Lua.</param>
/// <returns>
/// A named source or bytecode chunk to load, or <see langword="null"/> when the
/// module is not available.
/// </returns>
/// <remarks>
/// Resolvers are synchronous in this preview slice and must not perform
/// arbitrary filesystem loading by default. Return chunks from host-approved
/// source or bytecode caches. PopLua validates module names before invoking the
/// resolver and caches returned module values per <see cref="Session"/>.
/// Returning <see langword="null"/> reports <c>module not found</c> to Lua.
/// Throwing from the resolver reports a resolver failure through the current
/// PopLua execution result.
/// </remarks>
/// <example>
/// <code>
/// var lua = Engine.Create(b => b.Require((ctx, name) =>
///     name == "util" ? Chunk.Code("return { message = function() return 'ok' end }", "module:util.lua") : null));
/// </code>
/// </example>
public delegate Chunk? ModuleResolver(ScriptContext context, string moduleName);

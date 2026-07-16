using System.Reflection;
using System.Runtime.InteropServices;

namespace PopLua.Interop.Native;

internal static class LibraryResolver
{
    internal const string ImportName = "poplua-lua";

    private static readonly object Sync = new();
    private static nint _handle;
    private static Language? _version;

    internal static Language Version
    {
        get
        {
            EnsureLoaded();
            return _version!.Value;
        }
    }

    internal static void EnsureLoaded()
    {
        if (_handle != 0)
            return;

        lock (Sync)
        {
            if (_handle != 0)
                return;

            System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(
                typeof(LibraryResolver).Assembly,
                Resolve
            );

            _handle = Load();
        }
    }

    private static nint Resolve(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath
    ) =>
        libraryName == ImportName ? _handle : 0;

    private static nint Load()
    {
        var requested = Environment.GetEnvironmentVariable("POPLUA_LUA_VERSION");
        var versions = requested switch
        {
            null or "" => new[] { Language.Lua55, Language.Lua54 },
            "5.4" or "54" => new[] { Language.Lua54 },
            "5.5" or "55" => new[] { Language.Lua55 },
            _ => throw new InvalidOperationException(
                "POPLUA_LUA_VERSION must be '5.4' or '5.5'."
            ),
        };

        foreach (var version in versions)
        {
            foreach (var candidate in CandidateNames(version))
            {
                if (!System.Runtime.InteropServices.NativeLibrary.TryLoad(candidate, out var handle))
                    continue;

                if (MatchesVersion(handle, version))
                {
                    _version = version;
                    return handle;
                }

                System.Runtime.InteropServices.NativeLibrary.Free(handle);
            }
        }

        throw CreateLoadException(requested);
    }

    internal static IReadOnlyList<string> CandidateNames(Language version) =>
        version switch
        {
            Language.Lua55 =>
            [
                "lua5.5",
                "lua55",
                "liblua5.5.so",
                "liblua5.5.dylib",
                "lua5.5.dll",
                "lua55.dll",
            ],
            Language.Lua54 =>
            [
                "lua5.4",
                "lua54",
                "liblua5.4.so",
                "liblua5.4.dylib",
                "lua5.4.dll",
                "lua54.dll",
            ],
            _ => [],
        };

    internal static DllNotFoundException CreateLoadException(string? requested)
    {
        var requestedText = requested is null or "" ? "Lua 5.5 or Lua 5.4" : $"Lua {requested}";
        return new DllNotFoundException(
            $"PopLua could not load a compatible {requestedText} native library. "
                + "Install Lua 5.5 or Lua 5.4, or set POPLUA_LUA_VERSION to the installed version."
        );
    }

    private static unsafe bool MatchesVersion(nint handle, Language version)
    {
        if (!System.Runtime.InteropServices.NativeLibrary.TryGetExport(handle, "lua_version", out var symbol))
            return false;

        var getVersion = (delegate* unmanaged[Cdecl]<nint, double>)symbol;
        var actual = getVersion(0);
        var expected = version == Language.Lua55 ? 505d : 504d;
        return Math.Abs(actual - expected) < double.Epsilon;
    }
}

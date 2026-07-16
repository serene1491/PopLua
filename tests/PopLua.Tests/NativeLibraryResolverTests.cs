using PopLua.Interop.Native;

namespace PopLua.Tests;

public sealed class NativeLibraryResolverTests
{
    [Fact]
    public void Lua55NamesCoverSupportedPlatforms()
    {
        Assert.Equal(
            [
                "lua5.5",
                "lua55",
                "liblua5.5.so",
                "liblua5.5.dylib",
                "lua5.5.dll",
                "lua55.dll",
            ],
            LibraryResolver.CandidateNames(Language.Lua55)
        );
    }

    [Fact]
    public void Lua54NamesCoverSupportedPlatforms()
    {
        Assert.Equal(
            [
                "lua5.4",
                "lua54",
                "liblua5.4.so",
                "liblua5.4.dylib",
                "lua5.4.dll",
                "lua54.dll",
            ],
            LibraryResolver.CandidateNames(Language.Lua54)
        );
    }

    [Theory]
    [InlineData(null, "Lua 5.5 or Lua 5.4")]
    [InlineData("", "Lua 5.5 or Lua 5.4")]
    [InlineData("5.5", "Lua 5.5")]
    [InlineData("5.4", "Lua 5.4")]
    public void MissingLibraryErrorIsActionable(string? requested, string expected)
    {
        var error = LibraryResolver.CreateLoadException(requested);

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.Contains("POPLUA_LUA_VERSION", error.Message, StringComparison.Ordinal);
    }
}

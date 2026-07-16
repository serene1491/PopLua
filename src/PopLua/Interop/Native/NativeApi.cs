using System.Runtime.InteropServices;

namespace PopLua.Interop.Native;

internal static unsafe partial class NativeApi
{
    private const string Library = LibraryResolver.ImportName;

    static NativeApi() => LibraryResolver.EnsureLoaded();

    internal static int RegistryIndex =>
        LibraryResolver.Version == Language.Lua55
            ? -(int.MaxValue / 2 + 1000)
            : -1001000;

    [LibraryImport(Library, EntryPoint = "luaL_newstate")]
    internal static partial nint NewState();

    [LibraryImport(Library, EntryPoint = "lua_newstate")]
    private static partial nint NewState54(
        delegate* unmanaged[Cdecl]<nint, nint, nuint, nuint, nint> allocator,
        nint userData
    );

    [LibraryImport(Library, EntryPoint = "lua_newstate")]
    private static partial nint NewState55(
        delegate* unmanaged[Cdecl]<nint, nint, nuint, nuint, nint> allocator,
        nint userData,
        uint seed
    );

    internal static nint NewState(
        delegate* unmanaged[Cdecl]<nint, nint, nuint, nuint, nint> allocator,
        nint userData
    ) =>
        LibraryResolver.Version == Language.Lua55
            ? NewState55(
                allocator,
                userData,
                unchecked((uint)Random.Shared.NextInt64())
            )
            : NewState54(allocator, userData);

    [LibraryImport(Library, EntryPoint = "luaL_openlibs")]
    private static partial void OpenLibs54(nint state);

    [LibraryImport(Library, EntryPoint = "luaL_openselectedlibs")]
    private static partial void OpenLibs55(nint state, int load, int preload);

    internal static void OpenLibs(nint state)
    {
        if (LibraryResolver.Version == Language.Lua55)
            OpenLibs55(state, load: -1, preload: 0);
        else
            OpenLibs54(state);
    }

    [LibraryImport(Library, EntryPoint = "luaopen_base")]
    internal static partial int OpenBase(nint state);

    [LibraryImport(Library, EntryPoint = "luaopen_package")]
    internal static partial int OpenPackage(nint state);

    [LibraryImport(Library, EntryPoint = "luaopen_table")]
    internal static partial int OpenTable(nint state);

    [LibraryImport(Library, EntryPoint = "luaopen_io")]
    internal static partial int OpenIo(nint state);

    [LibraryImport(Library, EntryPoint = "luaopen_os")]
    internal static partial int OpenOs(nint state);

    [LibraryImport(Library, EntryPoint = "luaopen_string")]
    internal static partial int OpenString(nint state);

    [LibraryImport(Library, EntryPoint = "luaopen_math")]
    internal static partial int OpenMath(nint state);

    [LibraryImport(Library, EntryPoint = "luaopen_utf8")]
    internal static partial int OpenUtf8(nint state);

    [LibraryImport(Library, EntryPoint = "lua_close")]
    internal static partial void Close(nint state);

    [LibraryImport(Library, EntryPoint = "lua_gettop")]
    internal static partial int GetTop(nint state);

    [LibraryImport(Library, EntryPoint = "lua_settop")]
    internal static partial void SetTop(nint state, int index);

    [LibraryImport(Library, EntryPoint = "lua_type")]
    internal static partial NativeType Type(nint state, int index);

    [LibraryImport(Library, EntryPoint = "lua_absindex")]
    internal static partial int AbsIndex(nint state, int index);

    [LibraryImport(Library, EntryPoint = "lua_next")]
    internal static partial int Next(nint state, int index);

    [LibraryImport(Library, EntryPoint = "lua_pushnil")]
    internal static partial void PushNil(nint state);

    [LibraryImport(Library, EntryPoint = "lua_pushboolean")]
    internal static partial void PushBoolean(nint state, int value);

    [LibraryImport(Library, EntryPoint = "lua_pushinteger")]
    internal static partial void PushInteger(nint state, long value);

    [LibraryImport(Library, EntryPoint = "lua_pushnumber")]
    internal static partial void PushNumber(nint state, double value);

    [LibraryImport(Library, EntryPoint = "lua_pushlstring")]
    internal static partial nint PushString(nint state, byte* value, nuint length);

    [LibraryImport(Library, EntryPoint = "lua_pushcclosure")]
    internal static partial void PushCClosure(nint state, delegate* unmanaged[Cdecl]<nint, int> function, int upvalues);

    [LibraryImport(Library, EntryPoint = "lua_pushvalue")]
    internal static partial void PushValue(nint state, int index);

    [LibraryImport(Library, EntryPoint = "lua_newuserdatauv")]
    internal static partial nint NewUserData(nint state, nuint size, int userValueCount);

    [LibraryImport(Library, EntryPoint = "lua_newthread")]
    internal static partial nint NewThread(nint state);

    [LibraryImport(Library, EntryPoint = "lua_toboolean")]
    internal static partial int ToBoolean(nint state, int index);

    [LibraryImport(Library, EntryPoint = "lua_tointegerx")]
    internal static partial long ToInteger(nint state, int index, int* isNumber);

    [LibraryImport(Library, EntryPoint = "lua_tonumberx")]
    internal static partial double ToNumber(nint state, int index, int* isNumber);

    [LibraryImport(Library, EntryPoint = "lua_tolstring")]
    internal static partial byte* ToString(nint state, int index, nuint* length);

    [LibraryImport(Library, EntryPoint = "luaL_loadbufferx")]
    internal static partial int LoadBuffer(nint state, byte* buffer, nuint size, byte* name, byte* mode);

    [LibraryImport(Library, EntryPoint = "lua_pcallk")]
    internal static partial int PCall(nint state, int args, int results, int errorFunction, nint context, nint continuation);

    [LibraryImport(Library, EntryPoint = "lua_resume")]
    internal static partial int Resume(nint state, nint from, int args, int* results);

    [LibraryImport(Library, EntryPoint = "lua_status")]
    internal static partial int Status(nint state);

    [LibraryImport(Library, EntryPoint = "lua_xmove")]
    internal static partial void XMove(nint from, nint to, int count);

    [LibraryImport(Library, EntryPoint = "lua_getglobal")]
    internal static partial NativeType GetGlobal(nint state, byte* name);

    [LibraryImport(Library, EntryPoint = "lua_setglobal")]
    internal static partial void SetGlobal(nint state, byte* name);

    [LibraryImport(Library, EntryPoint = "lua_createtable")]
    internal static partial void CreateTable(nint state, int arrayCount, int recordCount);

    [LibraryImport(Library, EntryPoint = "lua_setfield")]
    internal static partial void SetField(nint state, int index, byte* key);

    [LibraryImport(Library, EntryPoint = "lua_getfield")]
    internal static partial NativeType GetField(nint state, int index, byte* key);

    [LibraryImport(Library, EntryPoint = "lua_rawgeti")]
    internal static partial NativeType RawGetI(nint state, int index, long n);

    [LibraryImport(Library, EntryPoint = "lua_rawseti")]
    internal static partial void RawSetI(nint state, int index, long n);

    [LibraryImport(Library, EntryPoint = "lua_rawlen")]
    internal static partial nuint RawLen(nint state, int index);

    [LibraryImport(Library, EntryPoint = "lua_rotate")]
    internal static partial void Rotate(nint state, int index, int n);

    [LibraryImport(Library, EntryPoint = "lua_setmetatable")]
    internal static partial int SetMetaTable(nint state, int index);

    [LibraryImport(Library, EntryPoint = "luaL_newmetatable")]
    internal static partial int NewMetaTable(nint state, byte* name);

    [LibraryImport(Library, EntryPoint = "luaL_ref")]
    internal static partial int Ref(nint state, int index);

    [LibraryImport(Library, EntryPoint = "luaL_unref")]
    internal static partial void Unref(nint state, int index, int reference);

    [LibraryImport(Library, EntryPoint = "luaL_testudata")]
    internal static partial nint TestUserData(nint state, int index, byte* name);

    [LibraryImport(Library, EntryPoint = "luaopen_coroutine")]
    internal static partial int OpenCoroutine(nint state);

    [LibraryImport(Library, EntryPoint = "luaopen_debug")]
    internal static partial int OpenDebug(nint state);

    [LibraryImport(Library, EntryPoint = "lua_isinteger")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsInteger(nint state, int index);

    [LibraryImport(Library, EntryPoint = "lua_sethook")]
    internal static partial void SetHook(nint state, delegate* unmanaged[Cdecl]<nint, nint, void> hook, int mask, int count);

    [LibraryImport(Library, EntryPoint = "lua_gc")]
    internal static partial int Gc(nint state, int what);

    [LibraryImport(Library, EntryPoint = "lua_error")]
    internal static partial int Error(nint state);

    [LibraryImport(Library, EntryPoint = "lua_dump")]
    internal static partial int Dump(nint state, delegate* unmanaged[Cdecl]<nint, nint, nuint, nint, int> writer, nint data, int strip);
}

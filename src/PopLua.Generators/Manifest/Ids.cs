using System;

namespace PopLua.Generators.Manifest;

internal static class Ids
{
    public static string Module(string moduleName)
        => "module:" + RequireLuaName(moduleName, nameof(moduleName));

    public static string ModuleMember(string moduleName, string memberName)
        => Module(moduleName) + "." + RequireLuaName(memberName, nameof(memberName));

    public static string Userdata(string userdataName)
        => "userdata:" + RequireLuaName(userdataName, nameof(userdataName));

    public static string UserdataMember(string userdataName, string memberName)
        => Userdata(userdataName) + "." + RequireLuaName(memberName, nameof(memberName));

    public static string Descriptor(string descriptorName)
        => "descriptor:" + RequireLuaName(descriptorName, nameof(descriptorName));

    public static string DescriptorMember(string descriptorName, string memberName)
        => Descriptor(descriptorName) + "." + RequireLuaName(memberName, nameof(memberName));

    private static string RequireLuaName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Lua API ids require a non-empty Lua-facing name.", parameterName);

        return value;
    }
}

namespace PopLua.Generators.Manifest;

internal sealed class ApiType
{
    private ApiType(
        ApiTypeKind kind,
        string luaName,
        string? dotnetName,
        string? userdataId,
        string? userdataName,
        string? descriptorId,
        string? descriptorName,
        ApiType? elementType,
        bool allowsNil = false)
    {
        Kind = kind;
        LuaName = luaName;
        DotnetName = dotnetName;
        UserdataId = userdataId;
        UserdataName = userdataName;
        DescriptorId = descriptorId;
        DescriptorName = descriptorName;
        ElementType = elementType;
        AllowsNil = allowsNil;
    }

    public ApiTypeKind Kind { get; }
    public string LuaName { get; }
    public string? DotnetName { get; }
    public string? UserdataId { get; }
    public string? UserdataName { get; }
    public string? DescriptorId { get; }
    public string? DescriptorName { get; }
    public ApiType? ElementType { get; }
    public bool AllowsNil { get; }

    public static ApiType Nil { get; } = new(ApiTypeKind.Nil, "nil", null, null, null, null, null, null);
    public static ApiType Boolean { get; } = new(ApiTypeKind.Boolean, "boolean", null, null, null, null, null, null);
    public static ApiType Integer { get; } = new(ApiTypeKind.Integer, "integer", null, null, null, null, null, null);
    public static ApiType Number { get; } = new(ApiTypeKind.Number, "number", null, null, null, null, null, null);
    public static ApiType String { get; } = new(ApiTypeKind.String, "string", null, null, null, null, null, null);
    public static ApiType Function { get; } = new(ApiTypeKind.Function, "function", null, null, null, null, null, null);
    public static ApiType Value { get; } = new(ApiTypeKind.Value, "any", null, null, null, null, null, null);
    public static ApiType ValueArray { get; } = new(ApiTypeKind.ValueArray, "...", null, null, null, null, null, null);

    public static ApiType Userdata(string luaName, string? dotnetName = null)
        => new(ApiTypeKind.Userdata, luaName, dotnetName, Ids.Userdata(luaName), luaName, null, null, null);

    public static ApiType Descriptor(string luaName, string? dotnetName = null)
        => new(ApiTypeKind.Descriptor, luaName, dotnetName, null, null, Ids.Descriptor(luaName), luaName, null);

    public static ApiType DescriptorArray(ApiType elementType)
        => new(ApiTypeKind.DescriptorArray, elementType.LuaName + "[]", null, null, null, null, null, elementType);

    public static ApiType Array(ApiType elementType)
        => new(ApiTypeKind.Array, elementType.LuaName + "[]", null, null, null, null, null, elementType);

    public ApiType WithDotnetName(string dotnetName)
        => new(Kind, LuaName, dotnetName, UserdataId, UserdataName, DescriptorId, DescriptorName, ElementType, AllowsNil);

    public ApiType WithAllowsNil(bool allowsNil = true)
        => new(Kind, LuaName, DotnetName, UserdataId, UserdataName, DescriptorId, DescriptorName, ElementType, allowsNil);
}

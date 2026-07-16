using System.Linq;
using Microsoft.CodeAnalysis;

namespace PopLua.Generators.Manifest;

internal static class TypeMapper
{
    public static bool TryFromSymbol(ITypeSymbol type, out ApiType apiType)
    {
        var dotnetName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var mapped = type.SpecialType switch
        {
            SpecialType.System_Void => ApiType.Nil.WithDotnetName(dotnetName),
            SpecialType.System_Boolean => ApiType.Boolean.WithDotnetName(dotnetName),
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 => ApiType.Integer.WithDotnetName(dotnetName),
            SpecialType.System_Single or SpecialType.System_Double => ApiType.Number.WithDotnetName(dotnetName),
            SpecialType.System_String => ApiType.String.WithDotnetName(dotnetName),
            _ => null,
        };

        if (mapped is not null)
        {
            apiType = mapped;
            return true;
        }

        if (dotnetName == "global::PopLua.Marshaling.Value")
        {
            apiType = ApiType.Value.WithDotnetName(dotnetName);
            return true;
        }

        if (dotnetName == "global::PopLua.Runtime.FunctionRef")
        {
            apiType = ApiType.Function.WithDotnetName(dotnetName);
            return true;
        }

        if (type is IArrayTypeSymbol array
            && array.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::PopLua.Marshaling.Value")
        {
            apiType = ApiType.ValueArray.WithDotnetName(dotnetName);
            return true;
        }

        if (type is INamedTypeSymbol namedType)
        {
            var userdataAttribute = namedType.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "PopLua.Binding.UserdataAttribute");

            var luaName = userdataAttribute?.ConstructorArguments.Length > 0
                ? userdataAttribute.ConstructorArguments[0].Value as string
                : null;

            if (!string.IsNullOrWhiteSpace(luaName))
            {
                apiType = ApiType.Userdata(luaName!, dotnetName);
                return true;
            }
        }

        apiType = ApiType.Value;
        return false;
    }
}

using System;
using System.Linq;
using System.Text;

namespace PopLua.Generators.Manifest;

internal static class LuaLsEmitter
{
    public static string Emit(ApiManifest manifest)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---@meta");
        builder.AppendLine();

        foreach (var userdata in manifest.Userdata.OrderBy(u => u.Id, StringComparer.Ordinal))
            WriteUserdata(builder, userdata);

        foreach (var descriptor in manifest.Descriptors.OrderBy(d => d.Id, StringComparer.Ordinal))
            WriteDescriptor(builder, descriptor);

        foreach (var module in manifest.Modules.OrderBy(m => m.Id, StringComparer.Ordinal))
            WriteModule(builder, module);

        return builder.ToString();
    }

    private static void WriteModule(StringBuilder builder, ModuleModel module)
    {
        WriteSummary(builder, module.Documentation);
        builder.Append("---@class ").AppendLine(module.Name);
        foreach (var value in module.Values.OrderBy(v => v.Id, StringComparer.Ordinal))
            WriteField(builder, value);

        builder.Append(module.Name).AppendLine(" = {}");
        builder.AppendLine();

        foreach (var function in module.Functions.OrderBy(f => f.Id, StringComparer.Ordinal))
            WriteFunction(builder, module.Name + "." + function.Name, function, method: false);
    }

    private static void WriteUserdata(StringBuilder builder, UserdataModel userdata)
    {
        WriteSummary(builder, userdata.Documentation);
        builder.Append("---@class ").AppendLine(userdata.Name);
        foreach (var property in userdata.Properties.OrderBy(p => p.Id, StringComparer.Ordinal))
            WriteField(builder, property);

        foreach (var op in userdata.Operators.OrderBy(o => o.Id, StringComparer.Ordinal))
            WriteOperator(builder, op);

        builder.Append("local ").Append(userdata.Name).AppendLine(" = {}");
        builder.AppendLine();

        foreach (var method in userdata.Methods.OrderBy(m => m.Id, StringComparer.Ordinal))
            WriteFunction(builder, userdata.Name + ":" + method.Name, method, method: true);
    }

    private static void WriteDescriptor(StringBuilder builder, DescriptorModel descriptor)
    {
        WriteSummary(builder, descriptor.Documentation);
        builder.Append("---@class ").AppendLine(descriptor.Name);
        foreach (var field in descriptor.Fields.OrderBy(f => f.Id, StringComparer.Ordinal))
            WriteField(builder, field);

        builder.AppendLine();
    }

    private static void WriteFunction(StringBuilder builder, string qualifiedName, FunctionModel function, bool method)
    {
        WriteSummary(builder, function.Documentation);
        if (function.IsAsync)
        {
            builder.AppendLine("---@async");
            if (!function.PauseTime)
                builder.AppendLine("---Suspended time counts against PopLua active-time quota.");
        }

        var parameters = function.Parameters
            .Where(p => !p.IsContext)
            .ToArray();

        foreach (var parameter in parameters)
        {
            if (parameter.IsVariadic)
            {
                builder.Append("---@param ... any");
            }
            else
            {
                builder.Append("---@param ").Append(parameter.Name).Append(' ').Append(ToLuaLsType(parameter.Type));
            }

            AppendInlineDocumentation(builder, parameter.Documentation);
            builder.AppendLine();
        }

        foreach (var value in function.Returns)
        {
            if (value.Type.Kind == ApiTypeKind.ValueArray)
                builder.Append("---@return any ...");
            else
                builder.Append("---@return ").Append(ToLuaLsType(value.Type));

            AppendInlineDocumentation(builder, value.Documentation);
            builder.AppendLine();
        }

        builder.Append("function ").Append(qualifiedName).Append('(');
        builder.Append(string.Join(", ", parameters.Select(p => p.IsVariadic ? "..." : p.Name)));
        builder.AppendLine(") end");
        builder.AppendLine();
    }

    private static void WriteField(StringBuilder builder, ValueModel value)
    {
        builder.Append("---@field ").Append(value.Name).Append(' ').Append(ToLuaLsType(value.Type));
        AppendInlineDocumentation(builder, value.Documentation.Summary);
        builder.AppendLine();
    }

    private static void WriteOperator(StringBuilder builder, OperatorModel op)
    {
        var operation = op.Metamethod switch
        {
            "__add" => "add",
            "__sub" => "sub",
            "__mul" => "mul",
            "__div" => "div",
            "__unm" => "unm",
            "__eq" => "eq",
            "__lt" => "lt",
            "__le" => "le",
            _ => null,
        };

        if (operation is null)
            return;

        builder.Append("---@operator ").Append(operation);
        if (op.Parameters.Count > 1)
            builder.Append('(').Append(ToLuaLsType(op.Parameters[1].Type)).Append(')');

        builder.Append(':').Append(ToLuaLsType(op.Return.Type)).AppendLine();
    }

    private static void WriteSummary(StringBuilder builder, Documentation documentation)
    {
        if (documentation.Summary is null)
        {
            WriteDocumentationLines(builder, documentation.Remarks);
            return;
        }

        WriteDocumentationLines(builder, documentation.Summary);
        WriteDocumentationLines(builder, documentation.Remarks);
    }

    private static void WriteDocumentationLines(StringBuilder builder, string? text)
    {
        if (text is null)
            return;

        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            builder.Append("---").AppendLine(line.Trim());
    }

    private static void AppendInlineDocumentation(StringBuilder builder, string? documentation)
    {
        if (!string.IsNullOrWhiteSpace(documentation))
            builder.Append(" # ").Append(documentation);
    }

    private static string ToLuaLsType(ApiType type)
    {
        var name = type.Kind switch
        {
            ApiTypeKind.Nil => "nil",
            ApiTypeKind.Boolean => "boolean",
            ApiTypeKind.Integer => "integer",
            ApiTypeKind.Number => "number",
            ApiTypeKind.String => "string",
            ApiTypeKind.Function => "function",
            ApiTypeKind.Value => "any",
            ApiTypeKind.ValueArray => "any[]",
            ApiTypeKind.Descriptor => type.DescriptorName ?? type.LuaName,
            ApiTypeKind.DescriptorArray => (type.ElementType is { } element ? ToLuaLsType(element) : "any") + "[]",
            ApiTypeKind.Array => (type.ElementType is { } item ? ToLuaLsType(item) : "any") + "[]",
            ApiTypeKind.Userdata => type.UserdataName ?? type.LuaName,
            _ => "any",
        };

        return type.AllowsNil && name != "nil" && name != "any"
            ? name + "|nil"
            : name;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PopLua.Generators.Manifest;

internal static class MarkdownEmitter
{
    public static string Emit(ApiManifest manifest)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# PopLua Lua API");
        builder.AppendLine();
        builder.Append("Schema `").Append(manifest.SchemaId).Append("` v").Append(manifest.SchemaVersion)
            .Append(", PopLua `").Append(manifest.PopLuaVersion).AppendLine("`.");
        builder.AppendLine();
        builder.AppendLine("Generated from C# PopLua binding declarations.");
        builder.AppendLine();

        WriteContents(builder, manifest);

        if (manifest.Modules.Count > 0)
        {
            builder.AppendLine("## Modules");
            builder.AppendLine();
            foreach (var module in manifest.Modules.OrderBy(m => m.Id, StringComparer.Ordinal))
                WriteModule(builder, module);
        }

        if (manifest.Userdata.Count > 0)
        {
            builder.AppendLine("## Userdata");
            builder.AppendLine();
            foreach (var userdata in manifest.Userdata.OrderBy(u => u.Id, StringComparer.Ordinal))
                WriteUserdata(builder, userdata);
        }

        if (manifest.Descriptors.Count > 0)
        {
            builder.AppendLine("## Descriptors");
            builder.AppendLine();
            foreach (var descriptor in manifest.Descriptors.OrderBy(d => d.Id, StringComparer.Ordinal))
                WriteDescriptor(builder, descriptor);
        }

        WriteNotes(builder, manifest);

        return builder.ToString();
    }

    private static void WriteContents(StringBuilder builder, ApiManifest manifest)
    {
        builder.AppendLine("## Contents");
        builder.AppendLine();

        if (manifest.Modules.Count > 0)
        {
            builder.AppendLine("- [Modules](#modules)");
            foreach (var module in manifest.Modules.OrderBy(m => m.Id, StringComparer.Ordinal))
                builder.Append("  - [`").Append(module.Name).Append("`](#module-").Append(Anchor(module.Name)).AppendLine(")");
        }

        if (manifest.Userdata.Count > 0)
        {
            builder.AppendLine("- [Userdata](#userdata)");
            foreach (var userdata in manifest.Userdata.OrderBy(u => u.Id, StringComparer.Ordinal))
                builder.Append("  - [`").Append(userdata.Name).Append("`](#userdata-").Append(Anchor(userdata.Name)).AppendLine(")");
        }

        if (manifest.Descriptors.Count > 0)
        {
            builder.AppendLine("- [Descriptors](#descriptors)");
            foreach (var descriptor in manifest.Descriptors.OrderBy(d => d.Id, StringComparer.Ordinal))
                builder.Append("  - [`").Append(descriptor.Name).Append("`](#descriptor-").Append(Anchor(descriptor.Name)).AppendLine(")");
        }

        if (UsesFunctionRefs(manifest) || UsesVariadics(manifest))
            builder.AppendLine("- [Notes](#notes)");

        builder.AppendLine();
    }

    private static void WriteModule(StringBuilder builder, ModuleModel module)
    {
        builder.Append("### Module `").Append(module.Name).AppendLine("`");
        builder.AppendLine();
        WriteDocumentation(builder, module.Documentation);
        if (module.Capability is not null)
            builder.Append("Capability: `").Append(module.Capability).AppendLine("`").AppendLine();

        if (module.Values.Count > 0)
        {
            builder.AppendLine("#### Values");
            builder.AppendLine();
            foreach (var value in module.Values.OrderBy(v => v.Id, StringComparer.Ordinal))
                WriteValue(builder, module.Name + "." + value.Name, value);
            builder.AppendLine();
        }

        if (module.Functions.Count > 0)
        {
            builder.AppendLine("#### Functions");
            builder.AppendLine();
            foreach (var function in module.Functions.OrderBy(f => f.Id, StringComparer.Ordinal))
                WriteFunction(builder, module.Name + "." + function.Name, function);
        }
    }

    private static void WriteUserdata(StringBuilder builder, UserdataModel userdata)
    {
        builder.Append("### Userdata `").Append(userdata.Name).AppendLine("`");
        builder.AppendLine();
        WriteDocumentation(builder, userdata.Documentation);

        if (userdata.Properties.Count > 0)
        {
            builder.AppendLine("#### Properties");
            builder.AppendLine();
            foreach (var property in userdata.Properties.OrderBy(p => p.Id, StringComparer.Ordinal))
                WriteValue(builder, userdata.Name + "." + property.Name, property);
            builder.AppendLine();
        }

        if (userdata.Methods.Count > 0)
        {
            builder.AppendLine("#### Methods");
            builder.AppendLine();
            foreach (var method in userdata.Methods.OrderBy(m => m.Id, StringComparer.Ordinal))
                WriteFunction(builder, userdata.Name + ":" + method.Name, method);
        }

        if (userdata.Operators.Count > 0)
        {
            builder.AppendLine("#### Operators");
            builder.AppendLine();
            foreach (var op in userdata.Operators.OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                builder.Append("- `").Append(op.Metamethod).Append("`: `")
                    .Append(ToDisplayType(op.Return.Type)).Append('`');
                WriteInline(builder, op.Documentation.Summary);
                builder.AppendLine();
            }

            builder.AppendLine();
        }
    }

    private static void WriteDescriptor(StringBuilder builder, DescriptorModel descriptor)
    {
        builder.Append("### Descriptor `").Append(descriptor.Name).AppendLine("`");
        builder.AppendLine();
        WriteDocumentation(builder, descriptor.Documentation);

        if (descriptor.Fields.Count == 0)
            return;

        builder.AppendLine("| Field | Type | Description |");
        builder.AppendLine("|---|---|---|");
        foreach (var field in descriptor.Fields.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            builder.Append("| `").Append(field.Name).Append("` | `")
                .Append(ToDisplayType(field.Type)).Append("` | ")
                .Append(EscapeTableCell(field.Documentation.Summary))
                .AppendLine(" |");
        }

        builder.AppendLine();
    }

    private static void WriteValue(StringBuilder builder, string qualifiedName, ValueModel value)
    {
        builder.Append("- `").Append(qualifiedName).Append("`: `").Append(ToDisplayType(value.Type)).Append('`');
        if (value.IsReadOnly)
            builder.Append(" read-only");

        WriteInline(builder, value.Documentation.Summary);
        builder.AppendLine();
    }

    private static void WriteFunction(StringBuilder builder, string qualifiedName, FunctionModel function)
    {
        builder.Append("##### `").Append(Signature(qualifiedName, function)).AppendLine("`");
        builder.AppendLine();
        WriteDocumentation(builder, function.Documentation);
        if (function.IsAsync)
        {
            builder.Append("Async: yes");
            if (!function.PauseTime)
                builder.Append("; suspended time counts against active-time quota");

            builder.AppendLine().AppendLine();
        }

        var documentedParameters = function.Parameters
            .Where(p => !p.IsContext && !string.IsNullOrWhiteSpace(p.Documentation))
            .ToArray();
        if (documentedParameters.Length > 0)
        {
            builder.AppendLine("Parameters:");
            builder.AppendLine();
            foreach (var parameter in documentedParameters)
                builder.Append("- `").Append(parameter.IsVariadic ? "..." : parameter.Name).Append("` — ")
                    .AppendLine(parameter.Documentation);
            builder.AppendLine();
        }

        var documentedReturns = function.Returns
            .Where(r => !string.IsNullOrWhiteSpace(r.Documentation))
            .ToArray();
        if (documentedReturns.Length > 0)
        {
            builder.AppendLine("Returns:");
            builder.AppendLine();
            foreach (var value in documentedReturns)
                builder.Append("- ").AppendLine(value.Documentation);
            builder.AppendLine();
        }

        if (function.Documentation.Exceptions.Count > 0)
        {
            builder.AppendLine("Exceptions:");
            builder.AppendLine();
            foreach (var exception in function.Documentation.Exceptions)
            {
                builder.Append("- ");
                if (exception.Cref is not null)
                    builder.Append('`').Append(exception.Cref).Append("` — ");

                builder.AppendLine(exception.Documentation);
            }

            builder.AppendLine();
        }
    }

    private static string Signature(string qualifiedName, FunctionModel function)
    {
        var parameters = function.Parameters
            .Where(p => !p.IsContext)
            .Select(p => p.IsVariadic
                ? "...: " + VariadicElementType(p.Type)
                : p.Name + ": " + ToDisplayType(p.Type));
        var signature = qualifiedName + "(" + string.Join(", ", parameters) + ")";

        if (function.Returns.Count == 0)
            return signature;

        var returns = function.Returns.Select(r => ReturnDisplayType(r.Type)).ToArray();
        return signature + ": " + string.Join(", ", returns);
    }

    private static void WriteDocumentation(StringBuilder builder, Documentation documentation)
    {
        WriteParagraph(builder, documentation.Summary);
        WriteParagraph(builder, documentation.Remarks);

        if (documentation.Examples.Count == 0)
            return;

        builder.AppendLine("Examples:");
        foreach (var example in documentation.Examples)
        {
            builder.AppendLine();
            builder.AppendLine("```lua");
            builder.AppendLine(example);
            builder.AppendLine("```");
        }

        builder.AppendLine();
    }

    private static void WriteNotes(StringBuilder builder, ApiManifest manifest)
    {
        var refs = UsesFunctionRefs(manifest);
        var variadics = UsesVariadics(manifest);
        if (!refs && !variadics)
            return;

        builder.AppendLine("## Notes");
        builder.AppendLine();
        if (refs)
            builder.AppendLine("- `function` parameters are session-owned Lua callbacks. They are not durable handles across session disposal.");
        if (variadics)
            builder.AppendLine("- Varargs are represented as `...: any`; userdata values can be inspected as opaque `Value` entries but are not typed userdata parameters.");
        builder.AppendLine();
    }

    private static void WriteParagraph(StringBuilder builder, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        builder.AppendLine(text).AppendLine();
    }

    private static void WriteInline(StringBuilder builder, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            builder.Append(" — ").Append(text);
    }

    private static bool UsesFunctionRefs(ApiManifest manifest)
        => AllFunctions(manifest).SelectMany(f => f.Parameters).Any(p => p.Type.Kind == ApiTypeKind.Function);

    private static bool UsesVariadics(ApiManifest manifest)
        => AllFunctions(manifest).Any(f => f.Parameters.Any(p => p.IsVariadic) || f.Returns.Any(r => r.Type.Kind == ApiTypeKind.ValueArray));

    private static IEnumerable<FunctionModel> AllFunctions(ApiManifest manifest)
        => manifest.Modules.SelectMany(m => m.Functions).Concat(manifest.Userdata.SelectMany(u => u.Methods));

    private static string ReturnDisplayType(ApiType type)
        => type.Kind == ApiTypeKind.ValueArray ? "...: any" : ToDisplayType(type);

    private static string VariadicElementType(ApiType type)
        => type.Kind == ApiTypeKind.ValueArray ? "any" : ToDisplayType(type);

    private static string ToDisplayType(ApiType type)
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
            ApiTypeKind.ValueArray => "...",
            ApiTypeKind.Descriptor => type.DescriptorName ?? type.LuaName,
            ApiTypeKind.DescriptorArray => (type.ElementType is { } element ? ToDisplayType(element) : "any") + "[]",
            ApiTypeKind.Array => (type.ElementType is { } item ? ToDisplayType(item) : "any") + "[]",
            ApiTypeKind.Userdata => type.UserdataName ?? type.LuaName,
            _ => "any",
        };

        return type.AllowsNil && name != "nil" && name != "any"
            ? name + " | nil"
            : name;
    }

    private static string Anchor(string text)
    {
        var builder = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant())
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '_' || ch == '-')
                builder.Append(ch);
        }

        return builder.Length == 0 ? "api" : builder.ToString();
    }

    private static string EscapeTableCell(string? text)
    {
        if (text is null || string.IsNullOrWhiteSpace(text))
            return "";

        return text.Replace("|", "\\|");
    }
}

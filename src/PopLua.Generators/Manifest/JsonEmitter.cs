using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PopLua.Generators.Manifest;

internal static class JsonEmitter
{
    public static string Emit(ApiManifest manifest)
    {
        var writer = new JsonWriter();
        writer.BeginObject();
        writer.Property("schema", manifest.SchemaId);
        writer.Property("manifestVersion", manifest.SchemaVersion);
        writer.Property("popluaVersion", manifest.PopLuaVersion);

        if (!string.IsNullOrWhiteSpace(manifest.AssemblyIdentity))
            writer.Property("assemblyIdentity", manifest.AssemblyIdentity);

        writer.PropertyName("modules");
        writer.BeginArray();
        foreach (var module in manifest.Modules.OrderBy(m => m.Id, StringComparer.Ordinal))
            WriteModule(writer, module);
        writer.EndArray();

        writer.PropertyName("userdata");
        writer.BeginArray();
        foreach (var userdata in manifest.Userdata.OrderBy(u => u.Id, StringComparer.Ordinal))
            WriteUserdata(writer, userdata);
        writer.EndArray();

        writer.PropertyName("descriptors");
        writer.BeginArray();
        foreach (var descriptor in manifest.Descriptors.OrderBy(d => d.Id, StringComparer.Ordinal))
            WriteDescriptor(writer, descriptor);
        writer.EndArray();
        writer.EndObject();
        return writer.ToString();
    }

    private static void WriteModule(JsonWriter writer, ModuleModel module)
    {
        writer.BeginObject();
        writer.Property("id", module.Id);
        writer.Property("name", module.Name);
        writer.PropertyIfNotNull("csName", module.CsName);
        writer.PropertyIfNotNull("capability", module.Capability);
        WriteSource(writer, module.Source);
        WriteDocumentation(writer, module.Documentation);

        writer.PropertyName("functions");
        writer.BeginArray();
        foreach (var function in module.Functions.OrderBy(f => f.Id, StringComparer.Ordinal))
            WriteFunction(writer, function);
        writer.EndArray();

        writer.PropertyName("values");
        writer.BeginArray();
        foreach (var value in module.Values.OrderBy(v => v.Id, StringComparer.Ordinal))
            WriteValue(writer, value);
        writer.EndArray();
        writer.EndObject();
    }

    private static void WriteDescriptor(JsonWriter writer, DescriptorModel descriptor)
    {
        writer.BeginObject();
        writer.Property("id", descriptor.Id);
        writer.Property("name", descriptor.Name);
        writer.PropertyIfNotNull("csName", descriptor.CsName);
        WriteSource(writer, descriptor.Source);
        WriteDocumentation(writer, descriptor.Documentation);

        writer.PropertyName("fields");
        writer.BeginArray();
        foreach (var field in descriptor.Fields.OrderBy(f => f.Id, StringComparer.Ordinal))
            WriteValue(writer, field);
        writer.EndArray();
        writer.EndObject();
    }

    private static void WriteUserdata(JsonWriter writer, UserdataModel userdata)
    {
        writer.BeginObject();
        writer.Property("id", userdata.Id);
        writer.Property("name", userdata.Name);
        writer.PropertyIfNotNull("csName", userdata.CsName);
        writer.Property("setters", userdata.Setters);
        writer.Property("toString", userdata.EmitsToString);
        writer.Property("gc", userdata.Gc);
        WriteSource(writer, userdata.Source);
        WriteDocumentation(writer, userdata.Documentation);

        writer.PropertyName("methods");
        writer.BeginArray();
        foreach (var method in userdata.Methods.OrderBy(m => m.Id, StringComparer.Ordinal))
            WriteFunction(writer, method);
        writer.EndArray();

        writer.PropertyName("properties");
        writer.BeginArray();
        foreach (var property in userdata.Properties.OrderBy(p => p.Id, StringComparer.Ordinal))
            WriteValue(writer, property);
        writer.EndArray();

        writer.PropertyName("operators");
        writer.BeginArray();
        foreach (var op in userdata.Operators.OrderBy(o => o.Id, StringComparer.Ordinal))
            WriteOperator(writer, op);
        writer.EndArray();
        writer.EndObject();
    }

    private static void WriteFunction(JsonWriter writer, FunctionModel function)
    {
        writer.BeginObject();
        writer.Property("id", function.Id);
        writer.Property("name", function.Name);
        writer.PropertyIfNotNull("csName", function.CsName);
        writer.Property("kind", function.Kind);
        writer.Property("async", function.IsAsync);
        if (function.IsAsync)
            writer.Property("pauseTime", function.PauseTime);
        writer.Property("static", function.IsStatic);
        WriteSource(writer, function.Source);
        WriteDocumentation(writer, function.Documentation);

        writer.PropertyName("parameters");
        writer.BeginArray();
        foreach (var parameter in function.Parameters)
            WriteParameter(writer, parameter);
        writer.EndArray();

        writer.PropertyName("returns");
        writer.BeginArray();
        foreach (var value in function.Returns)
            WriteReturn(writer, value);
        writer.EndArray();
        writer.EndObject();
    }

    private static void WriteValue(JsonWriter writer, ValueModel value)
    {
        writer.BeginObject();
        writer.Property("id", value.Id);
        writer.Property("name", value.Name);
        writer.PropertyIfNotNull("csName", value.CsName);
        writer.Property("kind", value.Kind);
        writer.Property("readOnly", value.IsReadOnly);
        writer.Property("writable", value.IsWritable);
        writer.PropertyName("type");
        WriteType(writer, value.Type);
        WriteSource(writer, value.Source);
        WriteDocumentation(writer, value.Documentation);
        writer.EndObject();
    }

    private static void WriteOperator(JsonWriter writer, OperatorModel op)
    {
        writer.BeginObject();
        writer.Property("id", op.Id);
        writer.Property("metamethod", op.Metamethod);
        writer.PropertyIfNotNull("csName", op.CsName);
        WriteSource(writer, op.Source);
        WriteDocumentation(writer, op.Documentation);

        writer.PropertyName("parameters");
        writer.BeginArray();
        foreach (var parameter in op.Parameters)
            WriteParameter(writer, parameter);
        writer.EndArray();

        writer.PropertyName("return");
        WriteReturn(writer, op.Return);
        writer.EndObject();
    }

    private static void WriteParameter(JsonWriter writer, ParameterModel parameter)
    {
        writer.BeginObject();
        writer.Property("name", parameter.Name);
        writer.Property("context", parameter.IsContext);
        writer.Property("variadic", parameter.IsVariadic);
        writer.PropertyName("type");
        WriteType(writer, parameter.Type);
        writer.PropertyIfNotNull("documentation", parameter.Documentation);
        writer.EndObject();
    }

    private static void WriteReturn(JsonWriter writer, ReturnModel value)
    {
        writer.BeginObject();
        writer.PropertyName("type");
        WriteType(writer, value.Type);
        writer.PropertyIfNotNull("documentation", value.Documentation);
        writer.EndObject();
    }

    private static void WriteType(JsonWriter writer, ApiType type)
    {
        writer.BeginObject();
        writer.Property("kind", ToJsonKind(type.Kind));
        writer.Property("luaName", type.LuaName);
        if (type.AllowsNil)
            writer.Property("nullable", true);
        writer.PropertyIfNotNull("dotnetName", type.DotnetName);
        writer.PropertyIfNotNull("userdataId", type.UserdataId);
        writer.PropertyIfNotNull("userdataName", type.UserdataName);
        writer.PropertyIfNotNull("descriptorId", type.DescriptorId);
        writer.PropertyIfNotNull("descriptorName", type.DescriptorName);
        if (type.ElementType is not null)
        {
            writer.PropertyName("elementType");
            WriteType(writer, type.ElementType);
        }
        writer.EndObject();
    }

    private static void WriteSource(JsonWriter writer, SourceSymbol? source)
    {
        if (source is null)
            return;

        writer.PropertyName("source");
        writer.BeginObject();
        writer.Property("dotnetDisplayName", source.DotnetDisplayName);
        writer.Property("metadataName", source.MetadataName);
        writer.PropertyIfNotNull("containingType", source.ContainingType);
        writer.PropertyIfNotNull("namespace", source.Namespace);
        writer.Property("assemblyName", source.AssemblyName);
        writer.EndObject();
    }

    private static void WriteDocumentation(JsonWriter writer, Documentation documentation)
    {
        if (documentation.IsEmpty)
            return;

        writer.PropertyName("documentation");
        writer.BeginObject();
        writer.PropertyIfNotNull("summary", documentation.Summary);
        writer.PropertyIfNotNull("remarks", documentation.Remarks);
        writer.PropertyIfNotNull("returns", documentation.Returns);
        if (documentation.Parameters.Count > 0)
        {
            writer.PropertyName("parameters");
            writer.BeginObject();
            foreach (var parameter in documentation.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
                writer.Property(parameter.Key, parameter.Value);
            writer.EndObject();
        }

        if (documentation.Examples.Count > 0)
        {
            writer.PropertyName("examples");
            writer.BeginArray();
            foreach (var example in documentation.Examples)
                writer.Value(example);
            writer.EndArray();
        }

        if (documentation.Exceptions.Count > 0)
        {
            writer.PropertyName("exceptions");
            writer.BeginArray();
            foreach (var exception in documentation.Exceptions)
            {
                writer.BeginObject();
                writer.PropertyIfNotNull("cref", exception.Cref);
                writer.Property("documentation", exception.Documentation);
                writer.EndObject();
            }

            writer.EndArray();
        }

        writer.EndObject();
    }

    private static string ToJsonKind(ApiTypeKind kind)
        => kind switch
        {
            ApiTypeKind.Nil => "nil",
            ApiTypeKind.Boolean => "boolean",
            ApiTypeKind.Integer => "integer",
            ApiTypeKind.Number => "number",
            ApiTypeKind.String => "string",
            ApiTypeKind.Function => "function",
            ApiTypeKind.Value => "lua-value",
            ApiTypeKind.ValueArray => "lua-value-array",
            ApiTypeKind.Descriptor => "descriptor",
            ApiTypeKind.DescriptorArray => "descriptor-array",
            ApiTypeKind.Array => "array",
            ApiTypeKind.Userdata => "userdata",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private sealed class JsonWriter
    {
        private readonly StringBuilder _builder = new();
        private readonly Stack<bool> _first = new();
        private bool _expectingPropertyValue;
        private int _depth;

        public void BeginObject()
        {
            BeforeValue();
            _builder.Append('{');
            _first.Push(true);
            _depth++;
        }

        public void EndObject()
        {
            _depth--;
            if (!_first.Peek())
                NewLine();

            _builder.Append('}');
            _first.Pop();
        }

        public void BeginArray()
        {
            BeforeValue();
            _builder.Append('[');
            _first.Push(true);
            _depth++;
        }

        public void EndArray()
        {
            _depth--;
            if (!_first.Peek())
                NewLine();

            _builder.Append(']');
            _first.Pop();
        }

        public void PropertyName(string name)
        {
            BeforeProperty();
            WriteString(name);
            _builder.Append(": ");
            _expectingPropertyValue = true;
        }

        public void Property(string name, string value)
        {
            PropertyName(name);
            _expectingPropertyValue = false;
            WriteString(value);
        }

        public void Property(string name, int value)
        {
            PropertyName(name);
            _expectingPropertyValue = false;
            _builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Property(string name, bool value)
        {
            PropertyName(name);
            _expectingPropertyValue = false;
            _builder.Append(value ? "true" : "false");
        }

        public void PropertyIfNotNull(string name, string? value)
        {
            if (value is not null)
                Property(name, value);
        }

        public void Value(string value)
        {
            BeforeValue();
            WriteString(value);
        }

        public override string ToString()
            => _builder.AppendLine().ToString();

        private void BeforeProperty()
        {
            if (!_first.Pop())
                _builder.Append(',');

            _first.Push(false);
            NewLine();
        }

        private void BeforeValue()
        {
            if (_expectingPropertyValue)
            {
                _expectingPropertyValue = false;
                return;
            }

            if (_first.Count == 0)
                return;

            if (!_first.Pop())
                _builder.Append(',');

            _first.Push(false);
            NewLine();
        }

        private void NewLine()
        {
            _builder.AppendLine();
            _builder.Append(' ', _depth * 2);
        }

        private void WriteString(string value)
        {
            _builder.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"':
                        _builder.Append("\\\"");
                        break;
                    case '\\':
                        _builder.Append("\\\\");
                        break;
                    case '\b':
                        _builder.Append("\\b");
                        break;
                    case '\f':
                        _builder.Append("\\f");
                        break;
                    case '\n':
                        _builder.Append("\\n");
                        break;
                    case '\r':
                        _builder.Append("\\r");
                        break;
                    case '\t':
                        _builder.Append("\\t");
                        break;
                    default:
                        if (ch < ' ')
                        {
                            _builder.Append("\\u");
                            _builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _builder.Append(ch);
                        }

                        break;
                }
            }

            _builder.Append('"');
        }
    }
}

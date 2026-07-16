using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PopLua.Generators.Manifest;
using System.Text.Json;

namespace PopLua.Tests;

public sealed class ManifestFoundationTests
{
    [Fact]
    public void TypeMapperMapsSupportedTypes()
    {
        var type = GetTypeSymbol("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("vec2")]
            public partial class Vec2
            {
            }

            public static class Api
            {
                public static void VoidValue() {}
                public static bool BoolValue() => true;
                public static int IntValue() => 1;
                public static uint UIntValue() => 1;
                public static long LongValue() => 1;
                public static ulong ULongValue() => 1;
                public static float FloatValue() => 1;
                public static double DoubleValue() => 1;
                public static string StringValue() => "";
                public static Value ValueValue() => Value.Nil;
                public static Value[] ValueArrayValue() => [];
                public static Vec2 UserdataValue() => new();
                public static decimal UnsupportedValue() => 1;
            }
            """, "Api");

        AssertKind(type, "VoidValue", ApiTypeKind.Nil);
        AssertKind(type, "BoolValue", ApiTypeKind.Boolean);
        AssertKind(type, "IntValue", ApiTypeKind.Integer);
        AssertKind(type, "UIntValue", ApiTypeKind.Integer);
        AssertKind(type, "LongValue", ApiTypeKind.Integer);
        AssertKind(type, "ULongValue", ApiTypeKind.Integer);
        AssertKind(type, "FloatValue", ApiTypeKind.Number);
        AssertKind(type, "DoubleValue", ApiTypeKind.Number);
        AssertKind(type, "StringValue", ApiTypeKind.String);
        AssertKind(type, "ValueValue", ApiTypeKind.Value);
        AssertKind(type, "ValueArrayValue", ApiTypeKind.ValueArray);

        var userdata = MapReturn(type, "UserdataValue");
        Assert.Equal(ApiTypeKind.Userdata, userdata.Kind);
        Assert.Equal("vec2", userdata.UserdataName);
        Assert.Equal("userdata:vec2", userdata.UserdataId);

        var unsupported = ReturnType(type, "UnsupportedValue");
        Assert.False(TypeMapper.TryFromSymbol(unsupported, out _));
    }

    [Fact]
    public void StableIdsUseLuaFacingNames()
    {
        Assert.Equal("module:mathx", Ids.Module("mathx"));
        Assert.Equal("module:mathx.add", Ids.ModuleMember("mathx", "add"));
        Assert.Equal("userdata:vec2", Ids.Userdata("vec2"));
        Assert.Equal("userdata:vec2.__add", Ids.UserdataMember("vec2", "__add"));
        Assert.Equal("descriptor:select_descriptor", Ids.Descriptor("select_descriptor"));
        Assert.Equal("descriptor:select_descriptor.placeholder", Ids.DescriptorMember("select_descriptor", "placeholder"));
    }

    [Fact]
    public void StableIdsRejectBlankNames()
    {
        Assert.Throws<ArgumentException>(() => Ids.Module(""));
        Assert.Throws<ArgumentException>(() => Ids.ModuleMember("mathx", " "));
        Assert.Throws<ArgumentException>(() => Ids.Userdata(""));
        Assert.Throws<ArgumentException>(() => Ids.UserdataMember("vec2", " "));
    }

    [Fact]
    public void ManifestModelCarriesSchemaConstantsAndDerivedIds()
    {
        var manifest = new ApiManifest("1.0.0-rc.1");
        var module = new ModuleModel("mathx", capability: "math.use");
        var userdata = new UserdataModel("vec2");

        manifest.Modules.Add(module);
        manifest.Userdata.Add(userdata);
        manifest.Descriptors.Add(new DescriptorModel("select_descriptor"));
        module.Functions.Add(new FunctionModel(Ids.ModuleMember("mathx", "add"), "add", isAsync: false));
        userdata.Properties.Add(new ValueModel(Ids.UserdataMember("vec2", "x"), "x", ApiType.Number, isWritable: false));

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("poplua.lua-api", manifest.SchemaId);
        Assert.Equal("1.0.0-rc.1", manifest.PopLuaVersion);
        Assert.Equal("module:mathx", module.Id);
        Assert.Equal("math.use", module.Capability);
        Assert.Equal("module:mathx.add", module.Functions.Single().Id);
        Assert.Equal("userdata:vec2", userdata.Id);
        Assert.Equal("userdata:vec2.x", userdata.Properties.Single().Id);
        Assert.Equal("descriptor:select_descriptor", manifest.Descriptors.Single().Id);
    }

    [Fact]
    public void JsonEmitterPreservesSchemaVersionIdsAndTypes()
    {
        var manifest = new ApiManifest("1.0.0-rc.1", "Fixture, Version=1.0.0.0");
        var module = new ModuleModel("mathx", capability: "math.use", csName: "MathModule");
        var function = new FunctionModel("module:mathx.add", "add", isAsync: false, csName: "Add");
        function.Parameters.Add(new ParameterModel("left", ApiType.Integer.WithDotnetName("global::System.Int64")));
        function.Returns.Add(new ReturnModel(ApiType.Integer.WithDotnetName("global::System.Int64")));
        module.Functions.Add(function);
        manifest.Modules.Add(module);

        var descriptor = new DescriptorModel("select_descriptor");
        descriptor.Fields.Add(new ValueModel("descriptor:select_descriptor.placeholder", "placeholder", ApiType.String, isWritable: true));
        manifest.Descriptors.Add(descriptor);

        var json = JsonEmitter.Emit(manifest);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("poplua.lua-api", root.GetProperty("schema").GetString());
        Assert.Equal(1, root.GetProperty("manifestVersion").GetInt32());
        Assert.Equal("1.0.0-rc.1", root.GetProperty("popluaVersion").GetString());
        Assert.Equal("module:mathx", root.GetProperty("modules")[0].GetProperty("id").GetString());
        Assert.Equal("module:mathx.add", root.GetProperty("modules")[0].GetProperty("functions")[0].GetProperty("id").GetString());
        Assert.Equal("integer", root.GetProperty("modules")[0].GetProperty("functions")[0].GetProperty("returns")[0].GetProperty("type").GetProperty("kind").GetString());
        Assert.Contains("\n  \"schema\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void LuaLsEmitterUsesManifestModel()
    {
        var manifest = new ApiManifest("1.0.0-rc.1");
        var userdata = new UserdataModel("vec2");
        userdata.Properties.Add(new ValueModel("userdata:vec2.x", "x", ApiType.Number, isWritable: false));
        userdata.Operators.Add(new OperatorModel(
            "userdata:vec2.__add",
            "__add",
            new ReturnModel(ApiType.Userdata("vec2"))));
        userdata.Operators[0].Parameters.Add(new ParameterModel("left", ApiType.Userdata("vec2")));
        userdata.Operators[0].Parameters.Add(new ParameterModel("right", ApiType.Userdata("vec2")));
        manifest.Userdata.Add(userdata);

        var module = new ModuleModel("vec", capability: null);
        var function = new FunctionModel("module:vec.new", "new", isAsync: false);
        function.Parameters.Add(new ParameterModel("x", ApiType.Number));
        function.Returns.Add(new ReturnModel(ApiType.Userdata("vec2")));
        module.Functions.Add(function);
        var variadic = new FunctionModel("module:vec.pack", "pack", isAsync: false);
        variadic.Parameters.Add(new ParameterModel("values", ApiType.ValueArray, isVariadic: true));
        variadic.Returns.Add(new ReturnModel(ApiType.ValueArray));
        module.Functions.Add(variadic);
        manifest.Modules.Add(module);

        var descriptor = new DescriptorModel("select_descriptor");
        descriptor.Fields.Add(new ValueModel("descriptor:select_descriptor.placeholder", "placeholder", ApiType.String, isWritable: true));
        manifest.Descriptors.Add(descriptor);

        var lua = LuaLsEmitter.Emit(manifest);

        Assert.Contains("---@meta", lua, StringComparison.Ordinal);
        Assert.Contains("---@class vec2", lua, StringComparison.Ordinal);
        Assert.Contains("---@field x number", lua, StringComparison.Ordinal);
        Assert.Contains("---@operator add(vec2):vec2", lua, StringComparison.Ordinal);
        Assert.Contains("---@return vec2", lua, StringComparison.Ordinal);
        Assert.Contains("function vec.new(x) end", lua, StringComparison.Ordinal);
        Assert.Contains("---@param ... any", lua, StringComparison.Ordinal);
        Assert.Contains("---@return any ...", lua, StringComparison.Ordinal);
        Assert.Contains("---@class select_descriptor", lua, StringComparison.Ordinal);
        Assert.Contains("---@field placeholder string", lua, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownEmitterUsesManifestModel()
    {
        var manifest = new ApiManifest("1.0.0-rc.1");
        var docs = new Documentation(
            "Creates vectors.",
            remarks: null,
            returns: "A vector.",
            new Dictionary<string, string> { ["x"] = "X coordinate." });
        var module = new ModuleModel("vec", capability: "math.use", documentation: new Documentation(
            "Vector helpers.",
            null,
            null,
            new Dictionary<string, string>()));
        var function = new FunctionModel("module:vec.new", "new", isAsync: false, documentation: docs);
        function.Parameters.Add(new ParameterModel("x", ApiType.Number, documentation: "X coordinate."));
        function.Returns.Add(new ReturnModel(ApiType.Userdata("vec2"), "A vector."));
        module.Functions.Add(function);
        manifest.Modules.Add(module);

        var descriptor = new DescriptorModel("select_descriptor");
        descriptor.Fields.Add(new ValueModel("descriptor:select_descriptor.placeholder", "placeholder", ApiType.String, isWritable: true));
        manifest.Descriptors.Add(descriptor);

        var markdown = MarkdownEmitter.Emit(manifest);

        Assert.Contains("# PopLua Lua API", markdown, StringComparison.Ordinal);
        Assert.Contains("## Contents", markdown, StringComparison.Ordinal);
        Assert.Contains("## Modules", markdown, StringComparison.Ordinal);
        Assert.Contains("### Module `vec`", markdown, StringComparison.Ordinal);
        Assert.Contains("Capability: `math.use`", markdown, StringComparison.Ordinal);
        Assert.Contains("- [`vec`](#module-vec)", markdown, StringComparison.Ordinal);
        Assert.Contains("##### `vec.new(x: number): vec2`", markdown, StringComparison.Ordinal);
        Assert.Contains("- `x` — X coordinate.", markdown, StringComparison.Ordinal);
        Assert.Contains("- A vector.", markdown, StringComparison.Ordinal);
        Assert.Contains("## Descriptors", markdown, StringComparison.Ordinal);
        Assert.Contains("- [`select_descriptor`](#descriptor-select_descriptor)", markdown, StringComparison.Ordinal);
        Assert.Contains("### Descriptor `select_descriptor`", markdown, StringComparison.Ordinal);
        Assert.Contains("| `placeholder` | `string` |", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Dynamic globals are not part", markdown, StringComparison.Ordinal);
    }

    private static void AssertKind(INamedTypeSymbol type, string methodName, ApiTypeKind expected)
        => Assert.Equal(expected, MapReturn(type, methodName).Kind);

    private static ApiType MapReturn(INamedTypeSymbol type, string methodName)
    {
        Assert.True(TypeMapper.TryFromSymbol(ReturnType(type, methodName), out var apiType));
        return apiType;
    }

    private static ITypeSymbol ReturnType(INamedTypeSymbol type, string methodName)
        => type.GetMembers(methodName).OfType<IMethodSymbol>().Single().ReturnType;

    private static INamedTypeSymbol GetTypeSymbol(string source, string typeName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat([MetadataReference.CreateFromFile(typeof(Engine).Assembly.Location)])
            .DistinctBy(r => r.Display)
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "ManifestFoundationTest",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(diagnostics);

        return compilation.GetTypeByMetadataName(typeName)
            ?? throw new InvalidOperationException("Test type was not found.");
    }
}

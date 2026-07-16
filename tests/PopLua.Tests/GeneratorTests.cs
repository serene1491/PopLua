using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PopLua.Generators;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PopLua.Tests;

public sealed class GeneratorTests
{
    [Fact]
    public void GeneratedBindingsWithoutUnsafeBlocksReportDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("mathx")]
            public partial class MathModule
            {
                [Fn("add")]
                public static long Add(long left, long right) => left + right;
            }
            """, allowUnsafe: false);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA010");
    }

    [Fact]
    public void NonPublicLuaFunctionReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("bad")]
            public partial class BadModule
            {
                [Fn]
                private static long Hidden() => 1;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA001");
    }

    [Fact]
    public void NonPartialLuaModuleReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("bad")]
            public class BadModule
            {
                [Fn]
                public static long Value() => 1;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA003");
    }

    [Fact]
    public void UnsupportedTablePropertyReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;

            [Module("bad")]
            public partial class BadModule
            {
                [Fn]
                public static BadResult Run() => new();
            }

            [Table]
            public sealed class BadResult
            {
                public System.DateTime CreatedAt { get; } = System.DateTime.UtcNow;
            }
            """);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PLUA002");
    }

    [Fact]
    public void DuplicateTableFieldsReportDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;

            [Module("bad")]
            public partial class BadModule
            {
                [Fn]
                public static BadResult Run() => new();
            }

            [Table]
            public sealed class BadResult
            {
                [Field("value")]
                public string First { get; } = "";

                [Field("value")]
                public string Second { get; } = "";
            }
            """);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PLUA008");
    }

    [Fact]
    public void SnakeCaseNameIsGenerated()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("users")]
            public partial class UserModule
            {
                [Fn]
                public static string GetUserName(string name) => name;
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.Contains("\"get_user_name\"", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateLuaNamesReportDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("dup")]
            public partial class DupModule
            {
                [Fn("x")]
                public static long A() => 1;

                [Fn("x")]
                public static long B() => 2;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA008");
    }

    [Fact]
    public void ContextParameterNotFirstReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("ctx")]
            public partial class CtxModule
            {
                [Fn("x")]
                public static long X(long value, [Context] ScriptContext ctx) => value;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA004");
    }

    [Fact]
    public void UnsupportedParameterReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("bad")]
            public partial class BadModule
            {
                [Fn("x")]
                public static long X(decimal value) => 1;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA002");
    }

    [Fact]
    public void InvalidAsyncReturnReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("async")]
            public partial class AsyncModule
            {
                [Fn("x", Async = true)]
                public static long X() => 1;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA005");
    }

    [Theory]
    [InlineData("Task")]
    [InlineData("Task<string>")]
    public void TaskAsyncReturnsReportDiagnostic(string returnType)
    {
        var result = RunGenerator($$"""
            using System.Threading.Tasks;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("async")]
            public partial class AsyncModule
            {
                [Fn("x", Async = true)]
                public static {{returnType}} X() => throw new System.NotImplementedException();
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA005");
    }

    [Fact]
    public void PauseTimeWithoutAsyncReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("bad")]
            public partial class BadModule
            {
                [Fn("wait", PauseTime = true)]
                public static void Wait() { }
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA012");
    }

    [Fact]
    public void AsyncLuaFunctionGeneratesAsyncRegistration()
    {
        var result = RunGenerator("""
            using System.Threading.Tasks;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("async")]
            public partial class AsyncModule
            {
                [Fn("fetch", Async = true)]
                public static ValueTask<string> Fetch(string id) => ValueTask.FromResult(id);
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.Contains("registration.AsyncModuleFunction(Name, \"fetch\", &__PopLua_Fetch);", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.CompleteAsync<", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.BeginAsync<", generated, StringComparison.Ordinal);
        Assert.Contains("private static int __PopLua_Push_Fetch", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AsyncVoidLuaFunctionGeneratesAsyncRegistration()
    {
        var result = RunGenerator("""
            using System.Threading.Tasks;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("async")]
            public partial class AsyncModule
            {
                [Fn("send", Async = true)]
                public static ValueTask Send() => ValueTask.CompletedTask;
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.Contains("registration.AsyncModuleFunction(Name, \"send\", &__PopLua_Send);", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.CompleteAsync(state);", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.BeginAsync(state, __poplua_task, pauseTime: true);", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorParameterGeneratesTableReader()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("ui")]
            public partial class UiModule
            {
                [Fn("select")]
                public static void Select(string id, SelectDescriptor descriptor) { }
            }

            public sealed class SelectDescriptor
            {
                public string? Placeholder { get; init; }
                public IReadOnlyList<SelectOptionDescriptor> Options { get; init; } = [];
            }

            public sealed class SelectOptionDescriptor
            {
                public string Label { get; init; } = "";
                public string Value { get; init; } = "";
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));

        Assert.Empty(result.Diagnostics);
        Assert.Contains("__PopLua_ReadDescriptor_SelectDescriptor", generated, StringComparison.Ordinal);
        Assert.Contains("__PopLua_ReadDescriptorList_SelectOptionDescriptor", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.PushField(state, index, \"placeholder\")", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.PushArrayItem(state, index, __poplua_i)", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorFieldCanUseExplicitLuaName()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("ui")]
            public partial class UiModule
            {
                [Fn("select")]
                public static void Select(SelectDescriptor descriptor) { }
            }

            public sealed class SelectDescriptor
            {
                [Field("maxItems")]
                public int MaxItems { get; init; }
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));

        Assert.Empty(result.Diagnostics);
        Assert.Contains("Marshaller.PushField(state, index, \"maxItems\")", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("Marshaller.PushField(state, index, \"max_items\")", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorSupportsNullableScalarFields()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("ui")]
            public partial class UiModule
            {
                [Fn("select")]
                public static void Select(SelectDescriptor descriptor) { }
            }

            public sealed class SelectDescriptor
            {
                [Field("hasItems")]
                public bool? HasItems { get; init; }
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));

        Assert.Empty(result.Diagnostics);
        Assert.Contains("Marshaller.PushField(state, index, \"hasItems\")", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.ReadBool(state, -1)", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void DescriptorStringListsGenerateDenseArrayReader()
    {
        var result = RunGenerator("""
            using System.Collections.Generic;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("ui")]
            public partial class UiModule
            {
                [Fn("select")]
                public static void Select(SelectDescriptor descriptor) { }

                [Fn("tags")]
                public static void Tags(IReadOnlyList<string> values) { }
            }

            public sealed class SelectDescriptor
            {
                public IReadOnlyList<string> Options { get; init; } = [];
                public IList<string> Labels { get; init; } = [];
                public List<string> Values { get; init; } = [];
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
        var outputCompilation = result.GeneratedSources.Aggregate(
            result.Compilation,
            static (current, source) => current.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                source.SourceText,
                new CSharpParseOptions(LanguageVersion.Preview))));
        var errors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(result.Diagnostics);
        Assert.Empty(errors);
        Assert.Contains("Marshaller.ReadStringList(state, -1, __poplua_path_Options)", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.ReadStringList(state, 1, \"string array\")", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AsyncUserdataMethodGeneratesAsyncWrapper()
    {
        var result = RunGenerator("""
            using System.Threading.Tasks;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("async_user")]
            public partial class AsyncUser
            {
                [Fn("wait", Async = true)]
                public ValueTask<string> Wait() => ValueTask.FromResult("ok");
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.Empty(result.Diagnostics);
        Assert.Contains("Marshaller.PushAsyncFunction(state, &__PopLua_Wait);", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.CompleteAsync<", generated, StringComparison.Ordinal);
        Assert.Contains("Marshaller.BeginAsync<", generated, StringComparison.Ordinal);
        Assert.Contains("private static int __PopLua_Push_Wait", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidAsyncUserdataReturnReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("bad_async_user")]
            public partial class BadAsyncUser
            {
                [Fn("wait", Async = true)]
                public string Wait() => "bad";
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA005");
    }

    [Fact]
    public void ValueArrayNotLastReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("bad")]
            public partial class BadModule
            {
                [Fn("x")]
                public static long X(Value[] values, long other) => 1;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA007");
    }

    [Fact]
    public void UserdataValueSelfParameterReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("player")]
            public partial class Player
            {
                [Fn("save")]
                public void Save(Value self)
                {
                }
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA011");
    }

    [Fact]
    public void NonPartialLuaUserdataReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("vec2")]
            public class Vec2
            {
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA006");
    }

    [Fact]
    public void DuplicateUserdataNamesReportDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("dup")]
            public partial class DupUserdata
            {
                [Fn("x")]
                public long A() => 1;

                [Fn("x")]
                public long B() => 2;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA008");
    }

    [Fact]
    public void LuaIgnoreSuppressesUserdataMembers()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("ignored")]
            public partial class IgnoredModule
            {
                [Fn("new")]
                public static IgnoredUserdata New() => new();
            }

            [Userdata("ignored")]
            public partial class IgnoredUserdata
            {
                [Ignore]
                [Fn("hidden")]
                public long Hidden() => 1;

                [Ignore]
                [Prop("secret")]
                public long Secret { get; set; }

                [Fn("shown")]
                public long Shown() => 2;
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.DoesNotContain("__PopLua_Hidden", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("__PopLua_Key_Hidden", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("__PopLua_Key_Secret", generated, StringComparison.Ordinal);
        Assert.Contains("__PopLua_Shown", generated, StringComparison.Ordinal);
        Assert.Contains("__PopLua_Key_Shown", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateUserdataOperatorMetamethodReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("vec2")]
            public partial class Vec2
            {
                public static Vec2 operator +(Vec2 left, Vec2 right) => left;
                public static Vec2 operator +(Vec2 left, long right) => left;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA008");
    }

    [Fact]
    public void IgnoredDuplicateUserdataOperatorIsSkipped()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("vec2")]
            public partial class Vec2
            {
                public static Vec2 operator +(Vec2 left, Vec2 right) => left;

                [Ignore]
                public static Vec2 operator +(Vec2 left, long right) => left;
            }
            """);

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "PLUA008");
    }

    [Fact]
    public void UnsupportedUserdataOperatorParameterReportsDiagnostic()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Userdata("vec2")]
            public partial class Vec2
            {
                public static Vec2 operator +(Vec2 left, decimal right) => left;
            }
            """);

        Assert.Contains(result.Diagnostics, d => d.Id == "PLUA002");
    }

    [Fact]
    public void UserdataSetterCodeIsGenerated()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("box")]
            public partial class BoxModule
            {
                [Fn("new")]
                public static Box New() => new();
            }

            [Userdata("box", Setters = true)]
            public partial class Box
            {
                [Prop("value")]
                public long Value { get; set; }
            }
            """);

        var generated = string.Join("\n", result.GeneratedSources.Select(s => s.SourceText.ToString()));
        Assert.Contains("__PopLua_NewIndex", generated, StringComparison.Ordinal);
        Assert.Contains("__poplua_self.Value = global::PopLua.Binding.Marshaller.ReadLong(state, 3);", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void SampleGeneratedCodeCompiles()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("calc")]
            public partial class CalcModule
            {
                [Fn("mul")]
                public static long Mul(long a, long b) => a * b;
            }
            """);

        var outputCompilation = result.RunResult.Results[0].GeneratedSources.Aggregate(
            result.Compilation,
            static (current, source) => current.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                source.SourceText,
                new CSharpParseOptions(LanguageVersion.Preview))));

        var diagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void SampleGeneratedUserdataCodeCompiles()
    {
        var result = RunGenerator("""
            using System;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("vec")]
            public partial class VecModule
            {
                [Fn("new")]
                public static Vec2 New(double x, double y) => new(x, y);
            }

            [Userdata("vec2")]
            public partial class Vec2(double x, double y)
            {
                [Prop("x", ReadOnly = true)]
                public double X { get; } = x;

                [Fn("length")]
                public double Length() => Math.Abs(X);

                public static Vec2 operator +(Vec2 left, Vec2 right)
                    => new(left.X + right.X, 0);
            }
            """);

        var outputCompilation = result.RunResult.Results[0].GeneratedSources.Aggregate(
            result.Compilation,
            static (current, source) => current.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                source.SourceText,
                new CSharpParseOptions(LanguageVersion.Preview))));

        var diagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ManifestJsonProviderIsGeneratedWhenEnabled()
    {
        var result = RunGenerator("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            /// <summary>Math helpers for Lua scripts.</summary>
            [Module("mathx", Cap = "math.use")]
            public partial class MathModule
            {
                /// <summary>Adds two numbers.</summary>
                /// <remarks>Use this for small integer sums exposed to Lua.</remarks>
                /// <param name="left">The left number.</param>
                /// <param name="right">The right number.</param>
                /// <returns>The sum.</returns>
                /// <example>
                /// <code>
                /// local total = mathx.add(20, 22)
                /// </code>
                /// </example>
                /// <exception cref="System.OverflowException">Thrown when the host implementation overflows.</exception>
                [Fn("add")]
                public static long Add(long left, long right) => left + right;

                [Fn("later", Async = true, PauseTime = false)]
                public static ValueTask<string> Later(string value) => ValueTask.FromResult(value);

                [Fn("new_vec")]
                public static Vec2 NewVec(double x, double y) => new(x, y);

                [Fn("select")]
                public static void Select(SelectDescriptor descriptor) { }

                /// <summary>Current display name.</summary>
                [Prop("display_name")]
                public static string? DisplayName([Context] ScriptContext context) => null;
            }

            public sealed class SelectDescriptor
            {
                public string? Placeholder { get; init; }
                public IReadOnlyList<string> Tags { get; init; } = [];
            }

            /// <summary>A 2D vector.</summary>
            [Userdata("vec2", Setters = true)]
            public partial class Vec2(double x, double y)
            {
                /// <summary>The x coordinate.</summary>
                [Prop("x")]
                public double X { get; set; } = x;

                /// <summary>Computes vector length.</summary>
                [Fn("length")]
                public double Length() => Math.Sqrt(X * X + y * y);

                [Fn("length_async", Async = true)]
                public ValueTask<double> LengthAsync() => ValueTask.FromResult(Length());

                public static Vec2 operator +(Vec2 left, Vec2 right) => left;
            }
            """, new Dictionary<string, string>
        {
            ["build_property.PopLuaGenerateApiManifest"] = "true",
        });

        var manifestSource = result.GeneratedSources.Single(s => s.HintName == "PopLuaApiManifest.g.cs").SourceText.ToString();
        var json = ExtractGeneratedString(manifestSource, "Json");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("poplua.lua-api", root.GetProperty("schema").GetString());
        Assert.Equal("1.0.0-rc.1", root.GetProperty("popluaVersion").GetString());

        var module = root.GetProperty("modules")[0];
        Assert.Equal("module:mathx", module.GetProperty("id").GetString());
        Assert.Equal("math.use", module.GetProperty("capability").GetString());
        Assert.Equal("Math helpers for Lua scripts.", module.GetProperty("documentation").GetProperty("summary").GetString());

        var functions = module.GetProperty("functions").EnumerateArray().ToArray();
        var add = functions.Single(f => f.GetProperty("name").GetString() == "add");
        Assert.Equal("module:mathx.add", add.GetProperty("id").GetString());
        Assert.False(add.GetProperty("async").GetBoolean());
        Assert.Equal("Adds two numbers.", add.GetProperty("documentation").GetProperty("summary").GetString());
        Assert.Equal("Use this for small integer sums exposed to Lua.", add.GetProperty("documentation").GetProperty("remarks").GetString());
        Assert.Equal("The left number.", add.GetProperty("parameters")[0].GetProperty("documentation").GetString());
        Assert.Equal("The sum.", add.GetProperty("returns")[0].GetProperty("documentation").GetString());
        Assert.Contains("mathx.add", add.GetProperty("documentation").GetProperty("examples")[0].GetString(), StringComparison.Ordinal);
        Assert.Equal("System.OverflowException", add.GetProperty("documentation").GetProperty("exceptions")[0].GetProperty("cref").GetString());
        Assert.Equal("Thrown when the host implementation overflows.", add.GetProperty("documentation").GetProperty("exceptions")[0].GetProperty("documentation").GetString());

        var later = functions.Single(f => f.GetProperty("name").GetString() == "later");
        Assert.True(later.GetProperty("async").GetBoolean());
        Assert.False(later.GetProperty("pauseTime").GetBoolean());
        Assert.Equal("string", later.GetProperty("returns")[0].GetProperty("type").GetProperty("kind").GetString());

        var displayName = module.GetProperty("values").EnumerateArray().Single(v => v.GetProperty("name").GetString() == "display_name");
        Assert.Equal("computed-property", displayName.GetProperty("kind").GetString());
        Assert.Equal("string", displayName.GetProperty("type").GetProperty("kind").GetString());
        Assert.True(displayName.GetProperty("type").GetProperty("nullable").GetBoolean());
        Assert.Equal("Current display name.", displayName.GetProperty("documentation").GetProperty("summary").GetString());

        var userdata = root.GetProperty("userdata")[0];
        Assert.Equal("userdata:vec2", userdata.GetProperty("id").GetString());
        Assert.True(userdata.GetProperty("setters").GetBoolean());
        var userdataFunctions = userdata.GetProperty("methods").EnumerateArray().ToArray();
        var lengthAsync = userdataFunctions.Single(f => f.GetProperty("name").GetString() == "length_async");
        Assert.True(lengthAsync.GetProperty("async").GetBoolean());
        Assert.True(lengthAsync.GetProperty("pauseTime").GetBoolean());
        Assert.Equal("number", lengthAsync.GetProperty("returns")[0].GetProperty("type").GetProperty("kind").GetString());
        Assert.Equal("userdata:vec2.__add", userdata.GetProperty("operators")[0].GetProperty("id").GetString());
        Assert.Equal("__add", userdata.GetProperty("operators")[0].GetProperty("metamethod").GetString());

        var descriptor = root.GetProperty("descriptors")[0];
        Assert.Equal("descriptor:select_descriptor", descriptor.GetProperty("id").GetString());
        Assert.Equal("select_descriptor", descriptor.GetProperty("name").GetString());
        var descriptorFields = descriptor.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Equal("placeholder", descriptorFields[0].GetProperty("name").GetString());
        Assert.Equal("string", descriptorFields[0].GetProperty("type").GetProperty("kind").GetString());
        Assert.Equal("tags", descriptorFields[1].GetProperty("name").GetString());
        Assert.Equal("array", descriptorFields[1].GetProperty("type").GetProperty("kind").GetString());
        Assert.Equal("string", descriptorFields[1].GetProperty("type").GetProperty("elementType").GetProperty("kind").GetString());
    }

    [Fact]
    public void LuaLsDefinitionProviderIsGeneratedFromManifestWhenEnabled()
    {
        var result = RunGenerator("""
            using System.Threading.Tasks;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("api")]
            public partial class ApiModule
            {
                /// <summary>Fetches a value.</summary>
                /// <remarks>Runs through PopLua's coroutine bridge.</remarks>
                /// <param name="id">Identifier to fetch.</param>
                /// <returns>The fetched value.</returns>
                [Fn("fetch", Async = true, PauseTime = false)]
                public static ValueTask<string> Fetch([Context] ScriptContext ctx, string id) => ValueTask.FromResult(id);

                [Fn("new_box")]
                public static Box NewBox(long value) => new(value);

                [Fn("on_click")]
                public static void OnClick(FunctionRef callback) { }

                [Fn("select")]
                public static void Select(SelectDescriptor descriptor) { }

                /// <summary>Current box if one exists.</summary>
                [Prop("current_box")]
                public static Box? CurrentBox([Context] ScriptContext ctx) => null;
            }

            public sealed class SelectDescriptor
            {
                public string? Placeholder { get; init; }
            }

            [Userdata("box")]
            public partial class Box(long value)
            {
                /// <summary>The boxed value.</summary>
                [Prop("value", ReadOnly = true)]
                public long Value { get; } = value;

                [Fn("get")]
                public long Get() => Value;
            }
            """, new Dictionary<string, string>
        {
            ["build_property.PopLuaGenerateLuaLsDefinitions"] = "true",
        });

        var source = result.GeneratedSources.Single(s => s.HintName == "PopLuaLuaLsDefinitions.g.cs").SourceText.ToString();
        var lua = ExtractGeneratedString(source, "Lua");

        Assert.Contains("---@meta", lua, StringComparison.Ordinal);
        Assert.Contains("---@class api", lua, StringComparison.Ordinal);
        Assert.Contains("---Fetches a value.", lua, StringComparison.Ordinal);
        Assert.Contains("---Runs through PopLua's coroutine bridge.", lua, StringComparison.Ordinal);
        Assert.Contains("---@async", lua, StringComparison.Ordinal);
        Assert.Contains("---Suspended time counts against PopLua active-time quota.", lua, StringComparison.Ordinal);
        Assert.Contains("---@param id string # Identifier to fetch.", lua, StringComparison.Ordinal);
        Assert.Contains("---@return string # The fetched value.", lua, StringComparison.Ordinal);
        Assert.Contains("function api.fetch(id) end", lua, StringComparison.Ordinal);
        Assert.Contains("---@param callback function", lua, StringComparison.Ordinal);
        Assert.Contains("function api.on_click(callback) end", lua, StringComparison.Ordinal);
        Assert.Contains("---@field current_box box|nil # Current box if one exists.", lua, StringComparison.Ordinal);
        Assert.Contains("---@class select_descriptor", lua, StringComparison.Ordinal);
        Assert.Contains("---@field placeholder string", lua, StringComparison.Ordinal);
        Assert.Contains("---@param descriptor select_descriptor", lua, StringComparison.Ordinal);
        Assert.Contains("---@class box", lua, StringComparison.Ordinal);
        Assert.Contains("---@field value integer # The boxed value.", lua, StringComparison.Ordinal);
        Assert.Contains("function box:get() end", lua, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiDocumentationProviderIsGeneratedFromManifestWhenEnabled()
    {
        var result = RunGenerator("""
            using System.Threading.Tasks;
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            /// <summary>Inventory APIs.</summary>
            [Module("inventory", Cap = "inventory.read")]
            public partial class InventoryModule
            {
                /// <summary>Gets an item name.</summary>
                /// <remarks>Returns a display name suitable for Lua UI scripts.</remarks>
                /// <param name="id">Item id.</param>
                /// <returns>The item name.</returns>
                /// <example>
                /// <code>
                /// local name = inventory.get_name(1)
                /// </code>
                /// </example>
                /// <exception cref="System.InvalidOperationException">Thrown when the host inventory is unavailable.</exception>
                [Fn("get_name")]
                public static string GetName(long id) => "name";

                /// <summary>Refreshes the host inventory.</summary>
                [Fn("refresh", Async = true, PauseTime = false)]
                public static ValueTask Refresh() => ValueTask.CompletedTask;

                /// <summary>Current inventory status.</summary>
                [Prop("status")]
                public static string? Status([Context] ScriptContext context) => null;
            }
            """, new Dictionary<string, string>
        {
            ["build_property.PopLuaGenerateApiDocs"] = "true",
        });

        var source = result.GeneratedSources.Single(s => s.HintName == "PopLuaApiDocs.g.cs").SourceText.ToString();
        var markdown = ExtractGeneratedString(source, "Markdown");

        Assert.Contains("# PopLua Lua API", markdown, StringComparison.Ordinal);
        Assert.Contains("## Contents", markdown, StringComparison.Ordinal);
        Assert.Contains("## Modules", markdown, StringComparison.Ordinal);
        Assert.Contains("### Module `inventory`", markdown, StringComparison.Ordinal);
        Assert.Contains("Inventory APIs.", markdown, StringComparison.Ordinal);
        Assert.Contains("Capability: `inventory.read`", markdown, StringComparison.Ordinal);
        Assert.Contains("- [`inventory`](#module-inventory)", markdown, StringComparison.Ordinal);
        Assert.Contains("##### `inventory.get_name(id: integer): string`", markdown, StringComparison.Ordinal);
        Assert.Contains("#### Values", markdown, StringComparison.Ordinal);
        Assert.Contains("- `inventory.status`: `string | nil` read-only", markdown, StringComparison.Ordinal);
        Assert.Contains("Current inventory status.", markdown, StringComparison.Ordinal);
        Assert.Contains("Returns a display name suitable for Lua UI scripts.", markdown, StringComparison.Ordinal);
        Assert.Contains("- `id` — Item id.", markdown, StringComparison.Ordinal);
        Assert.Contains("- The item name.", markdown, StringComparison.Ordinal);
        Assert.Contains("##### `inventory.refresh()`", markdown, StringComparison.Ordinal);
        Assert.Contains("Async: yes; suspended time counts against active-time quota", markdown, StringComparison.Ordinal);
        Assert.Contains("local name = inventory.get_name(1)", markdown, StringComparison.Ordinal);
        Assert.Contains("`System.InvalidOperationException` — Thrown when the host inventory is unavailable.", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("## Limitations", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Dynamic globals are not part of the generated API surface", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolingProvidersAreGeneratedByDefault()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("mathx")]
            public partial class MathModule
            {
                [Fn("add")]
                public static long Add(long left, long right) => left + right;
            }
            """);

        Assert.Contains(result.GeneratedSources, s => s.HintName == "PopLuaApiManifest.g.cs");
        Assert.Contains(result.GeneratedSources, s => s.HintName == "PopLuaLuaLsDefinitions.g.cs");
        Assert.Contains(result.GeneratedSources, s => s.HintName == "PopLuaApiDocs.g.cs");
    }

    [Fact]
    public void ToolingProvidersCanBeDisabled()
    {
        var result = RunGenerator("""
            using PopLua.Binding;
            using PopLua.Context;
            using PopLua.Marshaling;
            using PopLua.Runtime;
            using PopLua.Sandboxing;

            [Module("mathx")]
            public partial class MathModule
            {
                [Fn("add")]
                public static long Add(long left, long right) => left + right;
            }
            """, new Dictionary<string, string>
        {
            ["build_property.PopLuaGenerateApiManifest"] = "false",
            ["build_property.PopLuaGenerateLuaLsDefinitions"] = "false",
            ["build_property.PopLuaGenerateApiDocs"] = "false",
        });

        Assert.DoesNotContain(result.GeneratedSources, s => s.HintName == "PopLuaApiManifest.g.cs");
        Assert.DoesNotContain(result.GeneratedSources, s => s.HintName == "PopLuaLuaLsDefinitions.g.cs");
        Assert.DoesNotContain(result.GeneratedSources, s => s.HintName == "PopLuaApiDocs.g.cs");
    }

    private static GeneratorRunResult RunGenerator(
        string source,
        IReadOnlyDictionary<string, string>? options = null,
        bool allowUnsafe = true)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat([MetadataReference.CreateFromFile(typeof(Engine).Assembly.Location)])
            .DistinctBy(r => r.Display)
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "GeneratorTest",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe));

        var generator = new ModuleGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            optionsProvider: new TestAnalyzerConfigOptionsProvider(options ?? new Dictionary<string, string>()));
        driver = driver.RunGenerators(compilation);

        return new GeneratorRunResult(compilation, driver.GetRunResult());
    }

    private static string ExtractGeneratedString(string source, string name)
    {
        var match = Regex.Match(source, name + " = \"(?<value>.*)\";", RegexOptions.Singleline);
        Assert.True(match.Success, "Generated string constant was not found.");
        return Regex.Unescape(match.Groups["value"].Value);
    }

    private sealed class GeneratorRunResult
    {
        public GeneratorRunResult(Compilation compilation, GeneratorDriverRunResult runResult)
        {
            Compilation = compilation;
            RunResult = runResult;
        }

        public Compilation Compilation { get; }
        public GeneratorDriverRunResult RunResult { get; }
        public IEnumerable<Diagnostic> Diagnostics => RunResult.Diagnostics.Concat(RunResult.Results.SelectMany(r => r.Diagnostics));
        public IEnumerable<GeneratedSourceResult> GeneratedSources => RunResult.Results.SelectMany(r => r.GeneratedSources);
    }

    private sealed class TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> values)
        : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _options = new TestAnalyzerConfigOptions(values);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
            => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => _options;
    }

    private sealed class TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out var result))
            {
                value = result;
                return true;
            }

            value = string.Empty;
            return false;
        }

        public override IEnumerable<string> Keys
            => values.Keys;
    }
}

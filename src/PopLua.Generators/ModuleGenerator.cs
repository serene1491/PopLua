using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using PopLua.Generators.Helpers;
using PopLua.Generators.Manifest;

namespace PopLua.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class ModuleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider),
            static (context, source) => Execute(context, source.Left, source.Right));
    }

    private static void Execute(SourceProductionContext context, Compilation compilation, AnalyzerConfigOptionsProvider options)
    {
        var modules = new List<ModuleInfo>();
        var userdataTypes = new List<UserdataInfo>();
        var allowUnsafe = compilation.Options is CSharpCompilationOptions { AllowUnsafe: true };
        var reportedUnsafe = false;

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(context.CancellationToken);

            foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var symbol = model.GetDeclaredSymbol(typeDeclaration, context.CancellationToken);
                if (symbol is null)
                    continue;

                var userdataAttribute = GetAttribute(symbol, "PopLua.Binding.UserdataAttribute");
                if (userdataAttribute is not null)
                {
                    ReportUnsafeBlocksRequiredIfNeeded(context, compilation, typeDeclaration, allowUnsafe, ref reportedUnsafe);

                    if (!IsPartial(typeDeclaration))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.UserdataMustBePartial,
                            typeDeclaration.Identifier.GetLocation(),
                            symbol.Name));
                    }
                    else
                    {
                        var userdata = BuildUserdata(context, symbol, typeDeclaration, userdataAttribute);
                        if (userdata is not null)
                        {
                            context.AddSource($"{userdata.HintName}.PopLua.Userdata.g.cs", userdata.Source);
                            userdataTypes.Add(userdata);
                        }
                    }
                }

                var moduleAttribute = GetAttribute(symbol, "PopLua.Binding.ModuleAttribute");
                if (moduleAttribute is null)
                    continue;

                ReportUnsafeBlocksRequiredIfNeeded(context, compilation, typeDeclaration, allowUnsafe, ref reportedUnsafe);

                if (!IsPartial(typeDeclaration))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.ModuleMustBePartial,
                        typeDeclaration.Identifier.GetLocation(),
                        symbol.Name));
                    continue;
                }

                var module = BuildModule(context, symbol, typeDeclaration, moduleAttribute);
                if (module is not null)
                {
                    modules.Add(module);
                    context.AddSource($"{module.HintName}.PopLua.g.cs", module.Source);
                }
            }
        }

        var hasApi = modules.Count > 0 || userdataTypes.Count > 0;
        var generateManifest = ShouldGenerate(options, "PopLuaGenerateApiManifest");
        var generateLuaLs = ShouldGenerate(options, "PopLuaGenerateLuaLsDefinitions");
        var generateDocs = ShouldGenerate(options, "PopLuaGenerateApiDocs");
        if (hasApi && (generateManifest || generateLuaLs || generateDocs))
        {
            var manifest = BuildManifest(compilation, modules, userdataTypes);
            if (generateManifest)
            {
                context.AddSource(
                    "PopLuaApiManifest.g.cs",
                    BuildTextProvider("PopLuaApiManifestProvider", "Json", JsonEmitter.Emit(manifest)));
            }

            if (generateLuaLs)
            {
                context.AddSource(
                    "PopLuaLuaLsDefinitions.g.cs",
                    BuildTextProvider("PopLuaLuaLsDefinitionProvider", "Lua", LuaLsEmitter.Emit(manifest)));
            }

            if (generateDocs)
            {
                context.AddSource(
                    "PopLuaApiDocs.g.cs",
                    BuildTextProvider("PopDocumentationProvider", "Markdown", MarkdownEmitter.Emit(manifest)));
            }
        }
    }

    private static UserdataInfo? BuildUserdata(
        SourceProductionContext context,
        INamedTypeSymbol type,
        TypeDeclarationSyntax syntax,
        AttributeData userdataAttribute)
    {
        var luaName = GetConstructorString(userdataAttribute) ?? type.Name;
        var setters = GetNamedBool(userdataAttribute, "Setters");
        var toString = GetNamedBool(userdataAttribute, "ToString", defaultValue: true);
        var gc = GetNamedBool(userdataAttribute, "Gc", defaultValue: true);
        var methods = new List<MethodInfo>();
        var properties = new List<ValueMemberInfo>();
        var operators = new List<OperatorInfo>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var valid = true;

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (HasAttribute(method, "PopLua.Binding.IgnoreAttribute"))
                continue;

            var fnAttribute = GetAttribute(method, "PopLua.Binding.FnAttribute");
            if (fnAttribute is null)
                continue;

            if (method.DeclaredAccessibility != Accessibility.Public)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.NonPublicFunction,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                valid = false;
            }

            if (method.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    method.Locations.FirstOrDefault(),
                    "static userdata method"));
                valid = false;
            }

            var methodName = GetConstructorString(fnAttribute) ?? NameHelpers.ToSnakeCase(method.Name);
            if (!seenNames.Add(methodName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DuplicateLuaName,
                    method.Locations.FirstOrDefault(),
                    methodName,
                    luaName));
                valid = false;
            }

            var async = GetNamedBool(fnAttribute, "Async");
            var pauseTime = GetNamedBool(fnAttribute, "PauseTime", defaultValue: async);
            if (!async && pauseTime)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.PauseTimeRequiresAsync,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                valid = false;
            }

            ITypeSymbol? asyncResultType = null;
            if (async && !TryGetValueTaskResult(method.ReturnType, out asyncResultType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.AsyncReturnType,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                valid = false;
            }

            var parameters = new List<ParameterInfo>();
            for (var i = 0; i < method.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                var injectedContext = HasAttribute(parameter, "PopLua.Binding.ContextAttribute");

                if (injectedContext && i != 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.ContextMustBeFirst,
                        parameter.Locations.FirstOrDefault(),
                        method.Name));
                    valid = false;
                }

                if (IsValueArray(parameter.Type) && i != method.Parameters.Length - 1)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.VariadicMustBeLast,
                        parameter.Locations.FirstOrDefault(),
                        method.Name));
                    valid = false;
                }

                if (!injectedContext
                    && parameter.Name == "self"
                    && TypeKey(parameter.Type) == "Value")
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UserdataSelfParameter,
                        parameter.Locations.FirstOrDefault(),
                        method.Name));
                    valid = false;
                }

                if (!injectedContext && !IsSupportedParameter(parameter.Type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedType,
                        parameter.Locations.FirstOrDefault(),
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    valid = false;
                }

                parameters.Add(new ParameterInfo(parameter, parameter.Name, parameter.Type, injectedContext));
            }

            if (!async && !IsSupportedReturn(method.ReturnType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    method.Locations.FirstOrDefault(),
                    method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                valid = false;
            }

            if (async && asyncResultType is not null && !IsSupportedReturn(asyncResultType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    method.Locations.FirstOrDefault(),
                    asyncResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                valid = false;
            }

            methods.Add(new MethodInfo(method, method.Name, methodName, method.ReturnType, asyncResultType, parameters, isStatic: false, isAsync: async, pauseTime: pauseTime));
        }

        foreach (var member in type.GetMembers())
        {
            if (HasAttribute(member, "PopLua.Binding.IgnoreAttribute"))
                continue;

            var propAttribute = GetAttribute(member, "PopLua.Binding.PropAttribute");
            if (propAttribute is null)
                continue;

            ITypeSymbol? valueType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null,
            };

            if (valueType is null)
                continue;

            if (member.DeclaredAccessibility != Accessibility.Public)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    member.Locations.FirstOrDefault(),
                    "non-public userdata property"));
                valid = false;
            }

            if (IsStaticValueMember(member))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    member.Locations.FirstOrDefault(),
                    "static userdata property"));
                valid = false;
            }

            if (!IsSupportedParameter(valueType) || IsValueArray(valueType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    member.Locations.FirstOrDefault(),
                    valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                valid = false;
            }

            var memberName = GetConstructorString(propAttribute) ?? NameHelpers.ToSnakeCase(member.Name);
            if (!seenNames.Add(memberName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DuplicateLuaName,
                    member.Locations.FirstOrDefault(),
                    memberName,
                    luaName));
                valid = false;
            }

            var readOnly = GetNamedBool(propAttribute, "ReadOnly");
            var writable = !readOnly && type.TypeKind == TypeKind.Class && IsWritableValueMember(member);
            properties.Add(new ValueMemberInfo(member, member.Name, memberName, valueType, writable, kind: "property"));
        }

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (HasAttribute(method, "PopLua.Binding.IgnoreAttribute"))
                continue;

            if (method.MethodKind != MethodKind.UserDefinedOperator)
                continue;

            var metamethod = OperatorMetamethod(method);
            if (metamethod is null)
                continue;

            if (!seenNames.Add(metamethod))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DuplicateLuaName,
                    method.Locations.FirstOrDefault(),
                    metamethod,
                    luaName));
                valid = false;
            }

            if (!IsSupportedReturn(method.ReturnType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    method.Locations.FirstOrDefault(),
                    method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                valid = false;
                continue;
            }

            foreach (var parameter in method.Parameters)
            {
                if (!IsSupportedParameter(parameter.Type) || IsValueArray(parameter.Type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedType,
                        parameter.Locations.FirstOrDefault(),
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    valid = false;
                }
            }

            operators.Add(new OperatorInfo(method, method.Name, metamethod, method.ReturnType, method.Parameters.Select(p => p.Type).ToArray()));
        }

        valid &= ValidateTableContracts(
            context,
            methods.Select(method => method.AsyncResultType ?? method.ReturnType)
                .Concat(properties.Select(property => property.Type))
                .Concat(operators.Select(op => op.ReturnType))
        );

        if (!valid)
            return null;

        var descriptors = DescriptorDependencies(
            methods,
            properties.Select(property => property.Type)
                .Concat(operators.Select(op => op.ReturnType))
        ).ToArray();
        var source = BuildUserdataSource(type, syntax, luaName, setters, toString, gc, methods, properties, operators, descriptors);
        var model = BuildUserdataModel(type, luaName, setters, toString, gc, methods, properties, operators);
        return new UserdataInfo(GetHintName(type), source, model, descriptors.Select(BuildDescriptorModel).ToArray());
    }

    private static ModuleInfo? BuildModule(
        SourceProductionContext context,
        INamedTypeSymbol type,
        TypeDeclarationSyntax syntax,
        AttributeData moduleAttribute)
    {
        var moduleName = GetConstructorString(moduleAttribute) ?? type.Name;
        var moduleCap = GetNamedString(moduleAttribute, "Cap");
        var methods = new List<MethodInfo>();
        var values = new List<ValueMemberInfo>();
        var computedProperties = new List<ComputedPropertyInfo>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var valid = true;

        foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (HasAttribute(method, "PopLua.Binding.IgnoreAttribute"))
                continue;

            var fnAttribute = GetAttribute(method, "PopLua.Binding.FnAttribute");
            if (fnAttribute is null)
                continue;

            if (method.DeclaredAccessibility != Accessibility.Public)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.NonPublicFunction,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                valid = false;
            }

            var luaName = GetConstructorString(fnAttribute) ?? NameHelpers.ToSnakeCase(method.Name);
            if (!seenNames.Add(luaName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DuplicateLuaName,
                    method.Locations.FirstOrDefault(),
                    luaName,
                    moduleName));
                valid = false;
            }

            var async = GetNamedBool(fnAttribute, "Async");
            var pauseTime = GetNamedBool(fnAttribute, "PauseTime", defaultValue: async);
            if (!async && pauseTime)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.PauseTimeRequiresAsync,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                valid = false;
            }

            ITypeSymbol? asyncResultType = null;
            if (async && !TryGetValueTaskResult(method.ReturnType, out asyncResultType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.AsyncReturnType,
                    method.Locations.FirstOrDefault(),
                    method.Name));
                valid = false;
            }

            var parameters = new List<ParameterInfo>();
            for (var i = 0; i < method.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                var injectedContext = HasAttribute(parameter, "PopLua.Binding.ContextAttribute");

                if (injectedContext && i != 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.ContextMustBeFirst,
                        parameter.Locations.FirstOrDefault(),
                        method.Name));
                    valid = false;
                }

                if (IsValueArray(parameter.Type) && i != method.Parameters.Length - 1)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.VariadicMustBeLast,
                        parameter.Locations.FirstOrDefault(),
                        method.Name));
                    valid = false;
                }

                if (!injectedContext && !IsSupportedParameter(parameter.Type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedType,
                        parameter.Locations.FirstOrDefault(),
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    valid = false;
                }

                parameters.Add(new ParameterInfo(parameter, parameter.Name, parameter.Type, injectedContext));
            }

            var luaReturnType = async ? asyncResultType : method.ReturnType;
            if (luaReturnType is not null && !IsSupportedReturn(luaReturnType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    method.Locations.FirstOrDefault(),
                    luaReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                valid = false;
            }

            methods.Add(new MethodInfo(method, method.Name, luaName, method.ReturnType, asyncResultType, parameters, method.IsStatic, async, pauseTime));
        }

        foreach (var member in type.GetMembers())
        {
            if (HasAttribute(member, "PopLua.Binding.IgnoreAttribute"))
                continue;

            var constAttribute = GetAttribute(member, "PopLua.Binding.ConstAttribute");
            if (constAttribute is null)
                continue;

            ITypeSymbol? valueType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null,
            };

            if (valueType is null)
                continue;

            if (member.DeclaredAccessibility != Accessibility.Public || !IsStaticValueMember(member))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    member.Locations.FirstOrDefault(),
                    "non-public or instance constant"));
                valid = false;
            }

            if (!IsSupportedValueMember(valueType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    member.Locations.FirstOrDefault(),
                    valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                valid = false;
            }

            var luaName = GetConstructorString(constAttribute) ?? NameHelpers.ToSnakeCase(member.Name);
            if (!seenNames.Add(luaName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DuplicateLuaName,
                    member.Locations.FirstOrDefault(),
                    luaName,
                    moduleName));
                valid = false;
            }

            values.Add(new ValueMemberInfo(member, member.Name, luaName, valueType, kind: "constant"));
        }

        foreach (var member in type.GetMembers())
        {
            if (HasAttribute(member, "PopLua.Binding.IgnoreAttribute"))
                continue;

            var propAttribute = GetAttribute(member, "PopLua.Binding.PropAttribute");
            if (propAttribute is null)
                continue;

            if (member is IMethodSymbol computed)
            {
                if (computed.DeclaredAccessibility != Accessibility.Public)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedType,
                        computed.Locations.FirstOrDefault(),
                        "non-public module property"));
                    valid = false;
                }

                var propertyParameters = new List<ParameterInfo>();
                for (var i = 0; i < computed.Parameters.Length; i++)
                {
                    var parameter = computed.Parameters[i];
                    var injectedContext = HasAttribute(parameter, "PopLua.Binding.ContextAttribute");

                    if (!injectedContext || i != 0)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.UnsupportedType,
                            parameter.Locations.FirstOrDefault(),
                            "module computed properties may only declare an optional [Context] parameter"));
                        valid = false;
                    }

                    propertyParameters.Add(new ParameterInfo(parameter, parameter.Name, parameter.Type, injectedContext));
                }

                if (!IsSupportedComputedPropertyReturn(computed.ReturnType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedType,
                        computed.Locations.FirstOrDefault(),
                        computed.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                    valid = false;
                }

                var computedLuaName = GetConstructorString(propAttribute) ?? NameHelpers.ToSnakeCase(computed.Name);
                if (!seenNames.Add(computedLuaName))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.DuplicateLuaName,
                        computed.Locations.FirstOrDefault(),
                        computedLuaName,
                        moduleName));
                    valid = false;
                }

                computedProperties.Add(new ComputedPropertyInfo(computed, computed.Name, computedLuaName, computed.ReturnType, propertyParameters, computed.IsStatic));
                continue;
            }

            ITypeSymbol? valueType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null,
            };

            if (valueType is null)
                continue;

            if (member.DeclaredAccessibility != Accessibility.Public || !IsStaticValueMember(member))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    member.Locations.FirstOrDefault(),
                    "non-public or instance module property"));
                valid = false;
            }

            if (!IsSupportedValueMember(valueType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    member.Locations.FirstOrDefault(),
                    valueType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                valid = false;
            }

            var luaName = GetConstructorString(propAttribute) ?? NameHelpers.ToSnakeCase(member.Name);
            if (!seenNames.Add(luaName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DuplicateLuaName,
                    member.Locations.FirstOrDefault(),
                    luaName,
                    moduleName));
                valid = false;
            }

            values.Add(new ValueMemberInfo(member, member.Name, luaName, valueType, kind: ValueKind(member)));
        }

        valid &= ValidateTableContracts(
            context,
            methods.Select(method => method.AsyncResultType ?? method.ReturnType)
                .Concat(computedProperties.Select(property => property.ReturnType))
        );

        if (!valid)
            return null;

        var descriptors = DescriptorDependencies(
            methods,
            computedProperties.Select(property => property.ReturnType)
        ).ToArray();
        var source = BuildModuleSource(type, syntax, moduleName, moduleCap, methods, values, computedProperties, descriptors);
        var model = BuildModuleModel(type, moduleName, moduleCap, methods, values, computedProperties);
        return new ModuleInfo(GetHintName(type), type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), source, model, descriptors.Select(BuildDescriptorModel).ToArray());
    }

    private static string BuildUserdataSource(
        INamedTypeSymbol type,
        TypeDeclarationSyntax syntax,
        string luaName,
        bool setters,
        bool toString,
        bool gc,
        IReadOnlyList<MethodInfo> methods,
        IReadOnlyList<ValueMemberInfo> properties,
        IReadOnlyList<OperatorInfo> operators,
        IReadOnlyList<INamedTypeSymbol> descriptors)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace
            ? null
            : type.ContainingNamespace.ToDisplayString();

        var keyword = syntax.Kind() == SyntaxKind.StructDeclaration ? "struct" : "class";
        var accessibility = type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        var typeName = type.Name;
        var fqType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var metatableName = "PopLua.Userdata." + luaName;
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Runtime.CompilerServices;");
        builder.AppendLine("using System.Runtime.InteropServices;");

        if (ns is not null)
        {
            builder.Append("namespace ").Append(ns).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append(accessibility).Append(" unsafe partial ").Append(keyword).Append(' ').AppendLine(typeName);
        builder.AppendLine("{");
        builder.Append("    internal const string __PopLua_MetatableName = \"").Append(NameHelpers.Escape(metatableName)).AppendLine("\";");
        foreach (var method in methods)
            builder.Append("    private static readonly byte[] __PopLua_Key_")
                .Append(method.SymbolName)
                .Append(" = ")
                .Append(ByteArrayExpression(method.LuaName))
                .AppendLine(";");
        foreach (var property in properties)
            builder.Append("    private static readonly byte[] __PopLua_Key_")
                .Append(property.SymbolName)
                .Append(" = ")
                .Append(ByteArrayExpression(property.LuaName))
                .AppendLine(";");
        builder.AppendLine();
        builder.AppendLine("    internal static void __PopLua_Register(global::PopLua.Binding.Registration registration)");
        builder.AppendLine("    {");
        builder.AppendLine("        registration.UserdataMetatableFunction(__PopLua_MetatableName, \"__index\", &__PopLua_Index);");

        if (setters)
            builder.AppendLine("        registration.UserdataMetatableFunction(__PopLua_MetatableName, \"__newindex\", &__PopLua_NewIndex);");

        if (toString)
            builder.AppendLine("        registration.UserdataMetatableFunction(__PopLua_MetatableName, \"__tostring\", &__PopLua_ToString);");

        if (gc)
            builder.AppendLine("        registration.UserdataMetatableFunction(__PopLua_MetatableName, \"__gc\", &__PopLua_Gc);");

        foreach (var op in operators)
            builder.Append("        registration.UserdataMetatableFunction(__PopLua_MetatableName, \"")
                .Append(op.Metamethod)
                .Append("\", &__PopLua_")
                .Append(op.SymbolName)
                .AppendLine(");");

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    [ModuleInitializer]");
        builder.AppendLine("    internal static void __PopLua_RegisterGeneratedUserdata()");
        builder.Append("        => global::PopLua.Binding.GeneratedUserdataRegistry.Register(typeof(")
            .Append(fqType)
            .Append("), __PopLua_MetatableName, __PopLua_Register);")
            .AppendLine();
        builder.AppendLine();
        builder.AppendLine("    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]");
        builder.AppendLine("    private static int __PopLua_Index(nint state)");
        builder.AppendLine("    {");
        builder.AppendLine("        try");
        builder.AppendLine("        {");

        foreach (var method in methods)
        {
            builder.Append("            if (global::PopLua.Binding.Marshaller.StringEquals(state, 2, __PopLua_Key_")
                .Append(method.SymbolName)
                .AppendLine("))");
            builder.AppendLine("            {");
            builder.Append(method.IsAsync
                ? "                global::PopLua.Binding.Marshaller.PushAsyncFunction(state, &__PopLua_"
                : "                global::PopLua.Binding.Marshaller.PushFunction(state, &__PopLua_")
                .Append(method.SymbolName)
                .AppendLine(");");
            builder.AppendLine("                return 1;");
            builder.AppendLine("            }");
        }

        foreach (var property in properties)
        {
            builder.Append("            if (global::PopLua.Binding.Marshaller.StringEquals(state, 2, __PopLua_Key_")
                .Append(property.SymbolName)
                .AppendLine("))");
            builder.AppendLine("            {");
            builder.Append("                var __poplua_self_").Append(property.SymbolName)
                .Append(" = global::PopLua.Binding.Marshaller.ReadUserdata<").Append(fqType)
                .AppendLine(">(state, 1, __PopLua_MetatableName);");
            builder.Append("                ").AppendLine(PushStatement(property.Type, "__poplua_self_" + property.SymbolName + "." + property.SymbolName));
            builder.AppendLine("                return 1;");
            builder.AppendLine("            }");
        }

        builder.AppendLine("            global::PopLua.Binding.Marshaller.Push(state, global::PopLua.Marshaling.Value.Nil);");
        builder.AppendLine("            return 1;");
        builder.AppendLine("        }");
        builder.AppendLine("        catch (Exception ex)");
        builder.AppendLine("        {");
        builder.AppendLine("            return global::PopLua.Binding.Marshaller.Error(state, ex);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");

        if (setters)
        {
            builder.AppendLine();
            builder.AppendLine("    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]");
            builder.AppendLine("    private static int __PopLua_NewIndex(nint state)");
            builder.AppendLine("    {");
            builder.AppendLine("        try");
            builder.AppendLine("        {");
            builder.Append("            var __poplua_self = global::PopLua.Binding.Marshaller.ReadUserdata<").Append(fqType)
                .AppendLine(">(state, 1, __PopLua_MetatableName);");

            foreach (var property in properties.Where(p => p.IsWritable))
            {
                builder.Append("            if (global::PopLua.Binding.Marshaller.StringEquals(state, 2, __PopLua_Key_")
                    .Append(property.SymbolName)
                    .AppendLine("))");
                builder.AppendLine("            {");
                builder.Append("                __poplua_self.").Append(property.SymbolName).Append(" = ")
                    .Append(ReadExpression(property.Type, 3)).AppendLine(";");
                builder.AppendLine("                return 0;");
                builder.AppendLine("            }");
            }

            builder.AppendLine("            return global::PopLua.Binding.Marshaller.Error(state, \"Lua userdata property is read-only or unknown.\");");
            builder.AppendLine("        }");
            builder.AppendLine("        catch (Exception ex)");
            builder.AppendLine("        {");
            builder.AppendLine("            return global::PopLua.Binding.Marshaller.Error(state, ex);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
        }

        if (toString)
        {
            builder.AppendLine();
            builder.AppendLine("    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]");
            builder.AppendLine("    private static int __PopLua_ToString(nint state)");
            builder.AppendLine("    {");
            builder.AppendLine("        try");
            builder.AppendLine("        {");
            builder.Append("        var __poplua_self = global::PopLua.Binding.Marshaller.ReadUserdata<").Append(fqType)
                .AppendLine(">(state, 1, __PopLua_MetatableName);");
            builder.AppendLine("        global::PopLua.Binding.Marshaller.Push(state, __poplua_self.ToString() ?? string.Empty);");
            builder.AppendLine("        return 1;");
            builder.AppendLine("        }");
            builder.AppendLine("        catch (Exception ex)");
            builder.AppendLine("        {");
            builder.AppendLine("            return global::PopLua.Binding.Marshaller.Error(state, ex);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
        }

        if (gc)
        {
            builder.AppendLine();
            builder.AppendLine("    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]");
            builder.AppendLine("    private static int __PopLua_Gc(nint state)");
            builder.AppendLine("    {");
            builder.AppendLine("        try");
            builder.AppendLine("        {");
            builder.AppendLine("            return global::PopLua.Binding.Marshaller.FreeUserdata(state, 1, __PopLua_MetatableName);");
            builder.AppendLine("        }");
            builder.AppendLine("        catch");
            builder.AppendLine("        {");
            builder.AppendLine("            return 0;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
        }

        foreach (var method in methods)
            AppendUserdataMethod(builder, fqType, method);

        foreach (var op in operators)
            AppendOperator(builder, op);

        AppendDescriptorReaders(builder, descriptors);
        AppendTableWriters(builder, descriptors);

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendUserdataMethod(StringBuilder builder, string fqType, MethodInfo method)
    {
        builder.AppendLine();
        builder.AppendLine("    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]");
        builder.Append("    private static int __PopLua_").Append(method.SymbolName).AppendLine("(nint state)");
        builder.AppendLine("    {");
        builder.AppendLine("        try");
        builder.AppendLine("        {");
        builder.Append("            var __poplua_self = global::PopLua.Binding.Marshaller.ReadUserdata<").Append(fqType)
            .AppendLine(">(state, 1, __PopLua_MetatableName);");

        var luaIndex = 2;
        var argumentNames = new List<string>();
        foreach (var parameter in method.Parameters)
        {
            if (parameter.IsContext)
            {
                builder.Append("            var ").Append(parameter.Name).AppendLine(" = global::PopLua.Binding.Marshaller.Context(state);");
            }
            else
            {
                builder.Append("            var ").Append(parameter.Name).Append(" = ")
                    .Append(ReadExpression(parameter.Type, luaIndex)).AppendLine(";");
                if (!IsValueArray(parameter.Type))
                    luaIndex++;
            }

            argumentNames.Add(parameter.Name);
        }

        var call = "__poplua_self." + method.SymbolName + "(" + string.Join(", ", argumentNames) + ")";
        if (method.IsAsync)
        {
            builder.Append("            var __poplua_task = ").Append(call).AppendLine(";");
            if (method.AsyncResultType is null)
            {
                builder.AppendLine("            if (__poplua_task.IsCompletedSuccessfully)");
                builder.AppendLine("                return global::PopLua.Binding.Marshaller.CompleteAsync(state);");
                builder.Append("            return global::PopLua.Binding.Marshaller.BeginAsync(state, __poplua_task, pauseTime: ")
                    .Append(method.PauseTime ? "true" : "false")
                    .AppendLine(");");
            }
            else
            {
                builder.AppendLine("            if (__poplua_task.IsCompletedSuccessfully)");
                builder.Append("                return global::PopLua.Binding.Marshaller.CompleteAsync<")
                    .Append(method.AsyncResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Append(">(state, __poplua_task.GetAwaiter().GetResult(), __PopLua_Push_")
                    .Append(method.SymbolName)
                    .AppendLine(");");
                builder.Append("            return global::PopLua.Binding.Marshaller.BeginAsync<")
                    .Append(method.AsyncResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Append(">(state, __poplua_task, __PopLua_Push_")
                    .Append(method.SymbolName)
                    .Append(", pauseTime: ")
                    .Append(method.PauseTime ? "true" : "false")
                    .AppendLine(");");
            }
        }
        else if (method.ReturnType.SpecialType == SpecialType.System_Void)
        {
            builder.Append("            ").Append(call).AppendLine(";");
            builder.AppendLine("            return 0;");
        }
        else
        {
            builder.Append("            var result = ").Append(call).AppendLine(";");
            if (IsValueArray(method.ReturnType))
            {
                builder.AppendLine("            return global::PopLua.Binding.Marshaller.PushMany(state, result);");
            }
            else
            {
                builder.Append("            ").AppendLine(PushStatement(method.ReturnType, "result"));
                builder.AppendLine("            return 1;");
            }
        }

        builder.AppendLine("        }");
        builder.AppendLine("        catch (Exception ex)");
        builder.AppendLine("        {");
        builder.AppendLine(method.IsAsync
            ? "            return global::PopLua.Binding.Marshaller.BeginFailedAsync(state, ex);"
            : "            return global::PopLua.Binding.Marshaller.Error(state, ex);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");

        if (method.IsAsync && method.AsyncResultType is not null)
            AppendAsyncResultPusher(builder, method);
    }

    private static void AppendOperator(StringBuilder builder, OperatorInfo op)
    {
        builder.AppendLine();
        builder.AppendLine("    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]");
        builder.Append("    private static int __PopLua_").Append(op.SymbolName).AppendLine("(nint state)");
        builder.AppendLine("    {");
        builder.AppendLine("        try");
        builder.AppendLine("        {");

        for (var i = 0; i < op.Parameters.Count; i++)
            builder.Append("            var arg").Append(i).Append(" = ")
                .Append(ReadExpression(op.Parameters[i], i + 1)).AppendLine(";");

        builder.Append("            var result = ").Append(OperatorExpression(op)).AppendLine(";");
        builder.Append("            ").AppendLine(PushStatement(op.ReturnType, "result"));
        builder.AppendLine("            return 1;");
        builder.AppendLine("        }");
        builder.AppendLine("        catch (Exception ex)");
        builder.AppendLine("        {");
        builder.AppendLine("            return global::PopLua.Binding.Marshaller.Error(state, ex);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static string OperatorExpression(OperatorInfo op)
        => op.SymbolName switch
        {
            "op_Addition" => "arg0 + arg1",
            "op_Subtraction" => op.Parameters.Count == 1 ? "-arg0" : "arg0 - arg1",
            "op_Multiply" => "arg0 * arg1",
            "op_Division" => "arg0 / arg1",
            "op_UnaryNegation" => "-arg0",
            "op_Equality" => "arg0 == arg1",
            "op_LessThan" => "arg0 < arg1",
            "op_LessThanOrEqual" => "arg0 <= arg1",
            _ => throw new InvalidOperationException("Unsupported operator passed validation."),
        };

    private static string ByteArrayExpression(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return "new byte[] { " + string.Join(", ", bytes.Select(b => b.ToString(System.Globalization.CultureInfo.InvariantCulture))) + " }";
    }

    private static string? OperatorMetamethod(IMethodSymbol method)
        => method.Name switch
        {
            "op_Addition" => "__add",
            "op_Subtraction" => method.Parameters.Length == 1 ? "__unm" : "__sub",
            "op_Multiply" => "__mul",
            "op_Division" => "__div",
            "op_UnaryNegation" => "__unm",
            "op_Equality" => "__eq",
            "op_LessThan" => "__lt",
            "op_LessThanOrEqual" => "__le",
            _ => null,
        };

    private static string BuildModuleSource(
        INamedTypeSymbol type,
        TypeDeclarationSyntax syntax,
        string moduleName,
        string? moduleCap,
        IReadOnlyList<MethodInfo> methods,
        IReadOnlyList<ValueMemberInfo> values,
        IReadOnlyList<ComputedPropertyInfo> computedProperties,
        IReadOnlyList<INamedTypeSymbol> descriptors)
    {
        var ns = type.ContainingNamespace.IsGlobalNamespace
            ? null
            : type.ContainingNamespace.ToDisplayString();

        var keyword = syntax.Kind() == SyntaxKind.StructDeclaration ? "struct" : "class";
        var accessibility = type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
        var typeName = type.Name;
        var fqType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var createInstance = BuildCreateInstance(type, fqType);
        var builder = new StringBuilder();

        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Runtime.CompilerServices;");
        builder.AppendLine("using System.Runtime.InteropServices;");

        if (ns is not null)
        {
            builder.Append("namespace ").Append(ns).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append(accessibility).Append(" unsafe partial ").Append(keyword).Append(' ').Append(typeName)
            .AppendLine(" : global::PopLua.Binding.IGeneratedModule");
        builder.AppendLine("{");
        builder.Append("    public static string Name => \"").Append(NameHelpers.Escape(moduleName)).AppendLine("\";");
        builder.Append("    public static string? Cap => ");
        builder.Append(moduleCap is null ? "null" : $"\"{NameHelpers.Escape(moduleCap)}\"");
        builder.AppendLine(";");
        builder.AppendLine();
        builder.AppendLine("    public static void Register(global::PopLua.Binding.Registration registration)");
        builder.AppendLine("    {");

        foreach (var userdataType in UserdataDependencies(methods, computedProperties))
            builder.Append("        ").Append(userdataType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .AppendLine(".__PopLua_Register(registration);");

        foreach (var method in methods)
        {
            builder.Append(method.IsAsync ? "        registration.AsyncModuleFunction(Name, \"" : "        registration.ModuleFunction(Name, \"")
                .Append(NameHelpers.Escape(method.LuaName))
                .Append("\", &__PopLua_")
                .Append(method.SymbolName)
                .AppendLine(");");
        }

        foreach (var value in values)
            builder.Append("        registration.ModuleValue(Name, \"")
                .Append(NameHelpers.Escape(value.LuaName))
                .Append("\", ")
                .Append(ValueExpression(fqType, value))
                .AppendLine(");");

        if (computedProperties.Count > 0)
            builder.AppendLine("        registration.ModuleComputedProperties(Name, &__PopLua_Index);");

        builder.AppendLine("    }");

        builder.AppendLine();
        builder.AppendLine("    [ModuleInitializer]");
        builder.AppendLine("    internal static void __PopLua_RegisterGeneratedModule()");
        builder.Append("        => global::PopLua.Binding.GeneratedModuleRegistry.Register(typeof(")
            .Append(fqType)
            .Append("), Name, Cap, Register);")
            .AppendLine();

        if (createInstance is not null)
        {
            builder.AppendLine();
            builder.Append(createInstance);
        }

        if (computedProperties.Count > 0)
        {
            foreach (var property in computedProperties)
                builder.Append("    private static readonly byte[] __PopLua_Key_")
                    .Append(property.SymbolName)
                    .Append(" = ")
                    .Append(ByteArrayExpression(property.LuaName))
                    .AppendLine(";");

            builder.AppendLine();
            builder.AppendLine("    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]");
            builder.AppendLine("    private static int __PopLua_Index(nint state)");
            builder.AppendLine("    {");
            builder.AppendLine("        try");
            builder.AppendLine("        {");

            foreach (var property in computedProperties)
            {
                builder.Append("            if (global::PopLua.Binding.Marshaller.StringEquals(state, 2, __PopLua_Key_")
                    .Append(property.SymbolName)
                    .AppendLine("))");
                builder.AppendLine("            {");
                if (!property.IsStatic)
                {
                    builder.AppendLine("                var __poplua_ctx = global::PopLua.Binding.Marshaller.Context(state);");
                    builder.AppendLine("                var __poplua_instance = __PopLua_Create(__poplua_ctx);");
                }

                var argumentNames = new List<string>();
                foreach (var parameter in property.Parameters)
                {
                    if (parameter.IsContext)
                    {
                        if (property.IsStatic)
                            builder.Append("                var ").Append(parameter.Name).AppendLine(" = global::PopLua.Binding.Marshaller.Context(state);");
                        else
                            builder.Append("                var ").Append(parameter.Name).AppendLine(" = __poplua_ctx;");
                    }

                    argumentNames.Add(parameter.Name);
                }

                var receiver = property.IsStatic ? fqType : "__poplua_instance";
                builder.Append("                var result = ").Append(receiver).Append('.').Append(property.SymbolName)
                    .Append('(').Append(string.Join(", ", argumentNames)).AppendLine(");");
                builder.Append("                ").AppendLine(PushStatement(property.ReturnType, "result"));
                builder.AppendLine("                return 1;");
                builder.AppendLine("            }");
            }

            builder.AppendLine("            global::PopLua.Binding.Marshaller.Push(state, global::PopLua.Marshaling.Value.Nil);");
            builder.AppendLine("            return 1;");
            builder.AppendLine("        }");
            builder.AppendLine("        catch (Exception ex)");
            builder.AppendLine("        {");
            builder.AppendLine("            return global::PopLua.Binding.Marshaller.Error(state, ex);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
        }

        foreach (var method in methods)
        {
            builder.AppendLine();
            builder.AppendLine("    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]");
            builder.Append("    private static int __PopLua_").Append(method.SymbolName).AppendLine("(nint state)");
            builder.AppendLine("    {");
            builder.AppendLine("        try");
            builder.AppendLine("        {");
            if (!method.IsStatic)
            {
                builder.AppendLine("            var __poplua_ctx = global::PopLua.Binding.Marshaller.Context(state);");
                builder.AppendLine("            var __poplua_instance = __PopLua_Create(__poplua_ctx);");
            }

            var luaIndex = 1;
            var argumentNames = new List<string>();
            foreach (var parameter in method.Parameters)
            {
                if (parameter.IsContext)
                {
                    if (method.IsStatic)
                        builder.Append("            var ").Append(parameter.Name).AppendLine(" = global::PopLua.Binding.Marshaller.Context(state);");
                    else
                        builder.Append("            var ").Append(parameter.Name).AppendLine(" = __poplua_ctx;");
                }
                else
                {
                    builder.Append("            var ").Append(parameter.Name).Append(" = ")
                        .Append(ReadExpression(parameter.Type, luaIndex)).AppendLine(";");
                    if (!IsValueArray(parameter.Type))
                        luaIndex++;
                }

                argumentNames.Add(parameter.Name);
            }

            var receiver = method.IsStatic ? fqType : "__poplua_instance";
            var call = receiver + "." + method.SymbolName + "(" + string.Join(", ", argumentNames) + ")";
            if (method.IsAsync)
            {
                builder.Append("            var __poplua_task = ").Append(call).AppendLine(";");
                if (method.AsyncResultType is null)
                {
                    builder.AppendLine("            if (__poplua_task.IsCompletedSuccessfully)");
                    builder.AppendLine("                return global::PopLua.Binding.Marshaller.CompleteAsync(state);");
                    builder.Append("            return global::PopLua.Binding.Marshaller.BeginAsync(state, __poplua_task, pauseTime: ")
                        .Append(method.PauseTime ? "true" : "false")
                        .AppendLine(");");
                }
                else
                {
                    builder.AppendLine("            if (__poplua_task.IsCompletedSuccessfully)");
                    builder.Append("                return global::PopLua.Binding.Marshaller.CompleteAsync<")
                        .Append(method.AsyncResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append(">(state, __poplua_task.GetAwaiter().GetResult(), __PopLua_Push_")
                        .Append(method.SymbolName)
                        .AppendLine(");");
                    builder.Append("            return global::PopLua.Binding.Marshaller.BeginAsync<")
                        .Append(method.AsyncResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                        .Append(">(state, __poplua_task, __PopLua_Push_")
                        .Append(method.SymbolName)
                        .Append(", pauseTime: ")
                        .Append(method.PauseTime ? "true" : "false")
                        .AppendLine(");");
                }
            }
            else if (method.ReturnType.SpecialType == SpecialType.System_Void)
            {
                builder.Append("            ").Append(call).AppendLine(";");
                builder.AppendLine("            return 0;");
            }
            else
            {
                builder.Append("            var result = ").Append(call).AppendLine(";");
                if (IsValueArray(method.ReturnType))
                {
                    builder.AppendLine("            return global::PopLua.Binding.Marshaller.PushMany(state, result);");
                }
                else
                {
                    builder.Append("            ").AppendLine(PushStatement(method.ReturnType, "result"));
                    builder.AppendLine("            return 1;");
                }
            }

            builder.AppendLine("        }");
            builder.AppendLine("        catch (Exception ex)");
            builder.AppendLine("        {");
            builder.AppendLine(method.IsAsync
                ? "            return global::PopLua.Binding.Marshaller.BeginFailedAsync(state, ex);"
                : "            return global::PopLua.Binding.Marshaller.Error(state, ex);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");

            if (method.IsAsync && method.AsyncResultType is not null)
                AppendAsyncResultPusher(builder, method);
        }

        AppendDescriptorReaders(builder, descriptors);
        AppendTableWriters(builder, descriptors);

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AppendAsyncResultPusher(StringBuilder builder, MethodInfo method)
    {
        builder.AppendLine();
        builder.Append("    private static int __PopLua_Push_")
            .Append(method.SymbolName)
            .Append("(nint state, ")
            .Append(method.AsyncResultType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .AppendLine(" result)");
        builder.AppendLine("    {");

        if (IsValueArray(method.AsyncResultType))
        {
            builder.AppendLine("        return global::PopLua.Binding.Marshaller.PushMany(state, result);");
        }
        else
        {
            builder.Append("        ").AppendLine(PushStatement(method.AsyncResultType, "result"));
            builder.AppendLine("        return 1;");
        }

        builder.AppendLine("    }");
    }

    private static IEnumerable<ITypeSymbol> UserdataDependencies(
        IReadOnlyList<MethodInfo> methods,
        IReadOnlyList<ComputedPropertyInfo>? computedProperties = null)
    {
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var method in methods)
        {
            foreach (var dependency in UserdataDependencies(method.ReturnType, seen))
                yield return dependency;

            if (method.AsyncResultType is not null)
            {
                foreach (var dependency in UserdataDependencies(method.AsyncResultType, seen))
                    yield return dependency;
            }

            foreach (var parameter in method.Parameters)
            {
                foreach (var dependency in UserdataDependencies(parameter.Type, seen))
                    yield return dependency;
            }
        }

        if (computedProperties is null)
            yield break;

        foreach (var property in computedProperties)
        {
            foreach (var dependency in UserdataDependencies(property.ReturnType, seen))
                yield return dependency;
        }
    }

    private static IEnumerable<ITypeSymbol> UserdataDependencies(ITypeSymbol type, HashSet<ITypeSymbol> seen)
    {
        if (IsUserdata(type) && seen.Add(type))
        {
            yield return type;

            var userdata = (INamedTypeSymbol)type;
            foreach (var member in userdata.GetMembers())
            {
                if (member is IMethodSymbol method && GetAttribute(method, "PopLua.Binding.FnAttribute") is not null)
                {
                    var returnType = TryGetValueTaskResult(method.ReturnType, out var asyncResult)
                        ? asyncResult
                        : method.ReturnType;
                    if (returnType is not null)
                    {
                        foreach (var dependency in UserdataDependencies(returnType, seen))
                            yield return dependency;
                    }

                    foreach (var parameter in method.Parameters.Where(parameter => !HasAttribute(parameter, "PopLua.Binding.ContextAttribute")))
                    {
                        foreach (var dependency in UserdataDependencies(parameter.Type, seen))
                            yield return dependency;
                    }
                }
                else if (GetAttribute(member, "PopLua.Binding.PropAttribute") is not null)
                {
                    var memberType = member switch
                    {
                        IPropertySymbol property => property.Type,
                        IFieldSymbol field => field.Type,
                        IMethodSymbol propertyMethod => propertyMethod.ReturnType,
                        _ => null,
                    };
                    if (memberType is not null)
                    {
                        foreach (var dependency in UserdataDependencies(memberType, seen))
                            yield return dependency;
                    }
                }
            }
            yield break;
        }

        var descriptorType = DescriptorElementType(type)
            ?? TableElementType(type)
            ?? (IsDescriptor(type) || IsTable(type) ? (INamedTypeSymbol)type : null);
        if (descriptorType is null)
            yield break;

        foreach (var property in ContractProperties(descriptorType))
        {
            foreach (var dependency in UserdataDependencies(property.Type, seen))
                yield return dependency;
        }
    }

    private static IEnumerable<INamedTypeSymbol> DescriptorDependencies(
        IReadOnlyList<MethodInfo> methods,
        IEnumerable<ITypeSymbol>? additionalTypes = null)
    {
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var method in methods)
        {
            foreach (var parameter in method.Parameters.Where(p => !p.IsContext))
            {
                foreach (var descriptor in DescriptorDependencies(parameter.Type, seen))
                    yield return descriptor;
            }

            var returnType = method.AsyncResultType ?? method.ReturnType;
            foreach (var descriptor in DescriptorDependencies(returnType, seen))
                yield return descriptor;
        }

        if (additionalTypes is null)
            yield break;

        foreach (var type in additionalTypes)
        {
            foreach (var descriptor in DescriptorDependencies(type, seen))
                yield return descriptor;
        }
    }

    private static IEnumerable<INamedTypeSymbol> DescriptorDependencies(ITypeSymbol type, HashSet<ITypeSymbol> seen)
    {
        var descriptorType = DescriptorElementType(type)
            ?? TableElementType(type)
            ?? (IsDescriptor(type) || IsTable(type) ? (INamedTypeSymbol)type : null);
        if (descriptorType is null || !seen.Add(descriptorType))
            yield break;

        yield return descriptorType;

        foreach (var property in ContractProperties(descriptorType))
        {
            foreach (var nested in DescriptorDependencies(property.Type, seen))
                yield return nested;
        }
    }

    private static void AppendDescriptorReaders(StringBuilder builder, IReadOnlyList<INamedTypeSymbol> descriptors)
    {
        foreach (var descriptor in descriptors.OrderBy(d => d.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
        {
            if (!IsDescriptor(descriptor))
                continue;

            var fqType = descriptor.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var readerName = DescriptorReaderName(descriptor);
            var defaultName = "__poplua_default_" + DescriptorSafeName(descriptor);

            builder.AppendLine();
            builder.Append("    private static ").Append(fqType).Append(' ').Append(readerName).AppendLine("(nint state, int index, string path)");
            builder.AppendLine("    {");
            builder.AppendLine("        var __poplua_top = global::PopLua.Binding.Marshaller.Top(state);");
            builder.AppendLine("        try");
            builder.AppendLine("        {");
            builder.AppendLine("            global::PopLua.Binding.Marshaller.ExpectTable(state, index, path);");
            builder.Append("            global::PopLua.Binding.Marshaller.ValidateFields(state, index, path");
            foreach (var property in DescriptorProperties(descriptor))
                builder.Append(", \"").Append(NameHelpers.Escape(DescriptorFieldName(property))).Append('"');
            builder.AppendLine(");");
            builder.Append("            var ").Append(defaultName).Append(" = new ").Append(fqType);
            var requiredProperties = DescriptorProperties(descriptor).Where(IsRequired).ToArray();
            if (requiredProperties.Length == 0)
            {
                builder.AppendLine("();");
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine("            {");
                foreach (var property in requiredProperties)
                    builder.Append("                ").Append(property.Name).AppendLine(" = default!,");
                builder.AppendLine("            };");
            }

            foreach (var property in DescriptorProperties(descriptor))
            {
                var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var local = "__poplua_" + property.Name;
                var has = local + "_set";
                var luaName = DescriptorFieldName(property);
                var path = "__poplua_path_" + property.Name;

                builder.Append("            ").Append(propertyType).Append(' ').Append(local).AppendLine(" = default!;");
                builder.Append("            var ").Append(has).AppendLine(" = false;");
                builder.Append("            var ").Append(path).Append(" = path + \".").Append(NameHelpers.Escape(luaName)).AppendLine("\";");
                builder.Append("            if (global::PopLua.Binding.Marshaller.PushField(state, index, \"").Append(NameHelpers.Escape(luaName)).AppendLine("\"))");
                builder.AppendLine("            {");
                builder.AppendLine("                try");
                builder.AppendLine("                {");
                builder.Append("                    ").Append(local).Append(" = ").Append(ReadExpression(property.Type, -1, path)).AppendLine(";");
                builder.AppendLine("                }");
                builder.AppendLine("                catch (global::System.Exception ex)");
                builder.AppendLine("                {");
                builder.Append("                    throw new global::PopLua.Exceptions.ScriptException(\"invalid descriptor field: \" + ").Append(path).AppendLine(" + \": \" + ex.Message);");
                builder.AppendLine("                }");
                builder.Append("                ").Append(has).AppendLine(" = true;");
                builder.AppendLine("            }");
                builder.AppendLine("            global::PopLua.Binding.Marshaller.Pop(state);");
                if (IsRequired(property))
                    builder.Append("            if (!").Append(has).Append(") throw new global::PopLua.Exceptions.ScriptException(\"missing required descriptor field: \" + ").Append(path).AppendLine(");");
            }

            builder.Append("            return new ").Append(fqType).AppendLine();
            builder.AppendLine("            {");
            foreach (var property in DescriptorProperties(descriptor))
            {
                var local = "__poplua_" + property.Name;
                var has = local + "_set";
                builder.Append("                ").Append(property.Name).Append(" = ").Append(has)
                    .Append(" ? ").Append(local).Append(" : ").Append(defaultName).Append('.').Append(property.Name).AppendLine(",");
            }
            builder.AppendLine("            };");
            builder.AppendLine("        }");
            builder.AppendLine("        finally");
            builder.AppendLine("        {");
            builder.AppendLine("            global::PopLua.Binding.Marshaller.SetTop(state, __poplua_top);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");

            builder.AppendLine();
            builder.Append("    private static global::System.Collections.Generic.List<").Append(fqType).Append("> ")
                .Append(DescriptorListReaderName(descriptor)).AppendLine("(nint state, int index, string path)");
            builder.AppendLine("    {");
            builder.AppendLine("        var __poplua_count = global::PopLua.Binding.Marshaller.ValidateArray(state, index, path);");
            builder.Append("        var __poplua_items = new global::System.Collections.Generic.List<").Append(fqType).AppendLine(">(__poplua_count);");
            builder.AppendLine("        for (var __poplua_i = 1; __poplua_i <= __poplua_count; __poplua_i++)");
            builder.AppendLine("        {");
            builder.AppendLine("            global::PopLua.Binding.Marshaller.PushArrayItem(state, index, __poplua_i);");
            builder.Append("            __poplua_items.Add(").Append(readerName).AppendLine("(state, -1, path + \"[\" + __poplua_i.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + \"]\"));");
            builder.AppendLine("            global::PopLua.Binding.Marshaller.Pop(state);");
            builder.AppendLine("        }");
            builder.AppendLine("        return __poplua_items;");
            builder.AppendLine("    }");
        }
    }

    private static void AppendTableWriters(
        StringBuilder builder,
        IReadOnlyList<INamedTypeSymbol> descriptors)
    {
        foreach (var table in descriptors
            .Where(IsTable)
            .OrderBy(
                descriptor => descriptor.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                StringComparer.Ordinal))
        {
            var fqType = table.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var writerName = TableWriterName(table);
            var properties = TableProperties(table);

            builder.AppendLine();
            builder.Append("    private static void ").Append(writerName)
                .Append("(nint state, ").Append(fqType).AppendLine("? value, int depth)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (value is null)");
            builder.AppendLine("        {");
            builder.AppendLine("            global::PopLua.Binding.Marshaller.Push(state, global::PopLua.Marshaling.Value.Nil);");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine("        if (depth >= 64)");
            builder.AppendLine("            throw new global::PopLua.Exceptions.ScriptException(\"generated table output exceeds 64 nested levels\");");
            builder.Append("        global::PopLua.Binding.Marshaller.CreateTable(state, 0, ")
                .Append(properties.Count).AppendLine(");");
            foreach (var property in properties)
            {
                builder.Append("        ").AppendLine(
                    PushStatement(property.Type, "value." + property.Name, "depth + 1"));
                builder.Append("        global::PopLua.Binding.Marshaller.SetField(state, -2, \"")
                    .Append(NameHelpers.Escape(DescriptorFieldName(property))).AppendLine("\");");
            }
            builder.AppendLine("    }");

            builder.AppendLine();
            builder.Append("    private static void ").Append(TableListWriterName(table))
                .Append("(nint state, global::System.Collections.Generic.IEnumerable<")
                .Append(fqType).AppendLine(">? values, int depth)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (values is null)");
            builder.AppendLine("        {");
            builder.AppendLine("            global::PopLua.Binding.Marshaller.Push(state, global::PopLua.Marshaling.Value.Nil);");
            builder.AppendLine("            return;");
            builder.AppendLine("        }");
            builder.AppendLine("        global::PopLua.Binding.Marshaller.CreateTable(state, 0, 0);");
            builder.AppendLine("        var index = 1;");
            builder.AppendLine("        foreach (var value in values)");
            builder.AppendLine("        {");
            builder.Append("            ").Append(writerName).AppendLine("(state, value, depth + 1);");
            builder.AppendLine("            global::PopLua.Binding.Marshaller.SetArrayItem(state, -2, index++);");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
        }
    }

    private static ApiManifest BuildManifest(
        Compilation compilation,
        IReadOnlyList<ModuleInfo> modules,
        IReadOnlyList<UserdataInfo> userdata)
    {
        var manifest = new ApiManifest(
            ManifestConstants.PopLuaVersion,
            compilation.Assembly.Identity.GetDisplayName());

        foreach (var module in modules)
            manifest.Modules.Add(module.Model);

        foreach (var type in userdata)
            manifest.Userdata.Add(type.Model);

        var seenDescriptors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var descriptor in modules.SelectMany(m => m.Descriptors)
                     .Concat(userdata.SelectMany(u => u.Descriptors)))
        {
            if (seenDescriptors.Add(descriptor.Id))
                manifest.Descriptors.Add(descriptor);
        }

        return manifest;
    }

    private static string BuildTextProvider(string className, string propertyName, string value)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        builder.AppendLine("namespace PopLua.Generated;");
        builder.AppendLine();
        builder.Append("internal static class ").AppendLine(className);
        builder.AppendLine("{");
        builder.Append("    internal const string ").Append(propertyName).Append(" = \"")
            .Append(NameHelpers.Escape(value).Replace("\r", "\\r").Replace("\n", "\\n"))
            .AppendLine("\";");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static ModuleModel BuildModuleModel(
        INamedTypeSymbol type,
        string moduleName,
        string? capability,
        IReadOnlyList<MethodInfo> methods,
        IReadOnlyList<ValueMemberInfo> values,
        IReadOnlyList<ComputedPropertyInfo> computedProperties)
    {
        var module = new ModuleModel(
            moduleName,
            capability,
            type.Name,
            Source(type),
            DocumentationReader.FromSymbol(type));

        foreach (var method in methods)
            module.Functions.Add(BuildFunctionModel(
                method,
                Ids.ModuleMember(moduleName, method.LuaName),
                kind: "module-function"));

        foreach (var value in values)
            module.Values.Add(BuildValueModel(value, Ids.ModuleMember(moduleName, value.LuaName)));

        foreach (var property in computedProperties)
            module.Values.Add(BuildComputedPropertyModel(property, Ids.ModuleMember(moduleName, property.LuaName)));

        return module;
    }

    private static UserdataModel BuildUserdataModel(
        INamedTypeSymbol type,
        string userdataName,
        bool setters,
        bool toString,
        bool gc,
        IReadOnlyList<MethodInfo> methods,
        IReadOnlyList<ValueMemberInfo> properties,
        IReadOnlyList<OperatorInfo> operators)
    {
        var userdata = new UserdataModel(
            userdataName,
            type.Name,
            setters,
            toString,
            gc,
            Source(type),
            DocumentationReader.FromSymbol(type));

        foreach (var method in methods)
            userdata.Methods.Add(BuildFunctionModel(
                method,
                Ids.UserdataMember(userdataName, method.LuaName),
                kind: "userdata-method"));

        foreach (var property in properties)
            userdata.Properties.Add(BuildValueModel(property, Ids.UserdataMember(userdataName, property.LuaName)));

        foreach (var op in operators)
            userdata.Operators.Add(BuildOperatorModel(op, Ids.UserdataMember(userdataName, op.Metamethod)));

        return userdata;
    }

    private static DescriptorModel BuildDescriptorModel(INamedTypeSymbol type)
    {
        var descriptorName = DescriptorLuaName(type);
        var descriptor = new DescriptorModel(
            descriptorName,
            type.Name,
            Source(type),
            DocumentationReader.FromSymbol(type));

        foreach (var property in ContractProperties(type))
        {
            if (!TryMapApiType(property.Type, out var apiType))
                continue;

            descriptor.Fields.Add(new ValueModel(
                Ids.DescriptorMember(descriptorName, DescriptorFieldName(property)),
                DescriptorFieldName(property),
                apiType,
                isWritable: !IsTable(type),
                kind: "field",
                property.Name,
                Source(property),
                DocumentationReader.FromSymbol(property)));
        }

        return descriptor;
    }

    private static FunctionModel BuildFunctionModel(MethodInfo method, string id, string kind)
    {
        var documentation = DocumentationReader.FromSymbol(method.Symbol);
        var function = new FunctionModel(
            id,
            method.LuaName,
            method.IsAsync,
            method.SymbolName,
            method.IsStatic,
            method.PauseTime,
            kind,
            Source(method.Symbol),
            documentation);

        foreach (var parameter in method.Parameters)
        {
            if (!TryMapApiType(parameter.Type, out var parameterType))
                continue;

            function.Parameters.Add(new ParameterModel(
                parameter.Name,
                parameterType,
                parameter.IsContext,
                IsValueArray(parameter.Type),
                documentation.Parameters.TryGetValue(parameter.Name, out var parameterDocumentation)
                    ? parameterDocumentation
                    : null));
        }

        var returnType = method.IsAsync ? method.AsyncResultType : method.ReturnType;
        if (returnType is not null && returnType.SpecialType != SpecialType.System_Void)
        {
            if (TryMapApiType(returnType, out var apiReturnType))
                function.Returns.Add(new ReturnModel(apiReturnType, documentation.Returns));
        }

        return function;
    }

    private static ValueModel BuildValueModel(ValueMemberInfo value, string id)
    {
        TryMapApiType(value.Type, out var type);
        return new ValueModel(
            id,
            value.LuaName,
            type,
            value.IsWritable,
            value.Kind,
            value.SymbolName,
            Source(value.Symbol),
            DocumentationReader.FromSymbol(value.Symbol));
    }

    private static ValueModel BuildComputedPropertyModel(ComputedPropertyInfo property, string id)
    {
        TryMapApiType(property.ReturnType, out var type);
        return new ValueModel(
            id,
            property.LuaName,
            type,
            isWritable: false,
            kind: "computed-property",
            property.SymbolName,
            Source(property.Symbol),
            DocumentationReader.FromSymbol(property.Symbol));
    }

    private static OperatorModel BuildOperatorModel(OperatorInfo op, string id)
    {
        TryMapApiType(op.ReturnType, out var returnType);
        var documentation = DocumentationReader.FromSymbol(op.Symbol);
        var model = new OperatorModel(
            id,
            op.Metamethod,
            new ReturnModel(returnType, documentation.Returns),
            op.SymbolName,
            Source(op.Symbol),
            documentation);

        foreach (var parameter in op.Symbol.Parameters)
        {
            if (!TryMapApiType(parameter.Type, out var parameterType))
                continue;

            model.Parameters.Add(new ParameterModel(
                parameter.Name,
                parameterType,
                documentation: documentation.Parameters.TryGetValue(parameter.Name, out var parameterDocumentation)
                    ? parameterDocumentation
                    : null));
        }

        return model;
    }

    private static bool TryMapApiType(ITypeSymbol type, out ApiType apiType)
    {
        if (NullableValueType(type) is { } nullableValue && TypeMapper.TryFromSymbol(nullableValue, out apiType))
        {
            apiType = apiType.WithAllowsNil();
            return true;
        }

        if (TypeMapper.TryFromSymbol(type, out apiType))
        {
            apiType = ApplyNullability(type, apiType);
            return true;
        }

        if (IsStringList(type))
        {
            apiType = ApiType.Array(ApiType.String);
            return true;
        }

        if (TableElementType(type) is { } tableElement)
        {
            apiType = ApiType.DescriptorArray(ApiType.Descriptor(
                DescriptorLuaName(tableElement),
                tableElement.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            return true;
        }

        if (DescriptorElementType(type) is { } element)
        {
            apiType = ApiType.DescriptorArray(ApiType.Descriptor(DescriptorLuaName(element), element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            return true;
        }

        if (IsDescriptor(type) || IsTable(type))
        {
            var descriptor = (INamedTypeSymbol)type;
            apiType = ApiType.Descriptor(DescriptorLuaName(descriptor), descriptor.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            apiType = ApplyNullability(type, apiType);
            return true;
        }

        return false;
    }

    private static ApiType ApplyNullability(ITypeSymbol source, ApiType apiType)
        => source.NullableAnnotation == NullableAnnotation.Annotated
            ? apiType.WithAllowsNil()
            : apiType;

    private static SourceSymbol Source(ISymbol symbol)
    {
        var containingType = symbol.ContainingType;
        var ns = containingType?.ContainingNamespace ?? symbol.ContainingNamespace;
        return new SourceSymbol(
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            symbol.MetadataName,
            containingType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ns is null || ns.IsGlobalNamespace ? null : ns.ToDisplayString(),
            symbol.ContainingAssembly.Identity.GetDisplayName());
    }

    private static string? BuildCreateInstance(INamedTypeSymbol type, string fqType)
    {
        var needsInstanceFactory = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m => (GetAttribute(m, "PopLua.Binding.FnAttribute") is not null
                    || GetAttribute(m, "PopLua.Binding.PropAttribute") is not null)
                && !m.IsStatic);

        if (!needsInstanceFactory)
            return null;

        var constructors = type.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public)
            .OrderByDescending(c => c.Parameters.Length)
            .ToArray();

        var ctor = constructors.FirstOrDefault(c => c.Parameters.Length > 0)
            ?? constructors.FirstOrDefault(c => c.Parameters.Length == 0);

        if (ctor is null || ctor.Parameters.Length == 0)
            return $"    private static {fqType} __PopLua_Create(global::PopLua.Context.ScriptContext ctx) => new {fqType}();\n";

        var args = ctor.Parameters.Select(p =>
        {
            var typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var displayName = NameHelpers.Escape(typeName);
            return $"({typeName})(ctx.Services.GetService(typeof({typeName})) ?? throw new InvalidOperationException(\"Missing Lua module service: {displayName}\"))";
        });

        return $"    private static {fqType} __PopLua_Create(global::PopLua.Context.ScriptContext ctx) => new {fqType}({string.Join(", ", args)});\n";
    }

    private static string ReadExpression(ITypeSymbol type, int index, string? descriptorPathExpression = null)
        => TypeKey(type) switch
        {
            "bool" => $"global::PopLua.Binding.Marshaller.ReadBool(state, {index})",
            "int" => $"global::PopLua.Binding.Marshaller.ReadInt(state, {index})",
            "uint" => $"global::PopLua.Binding.Marshaller.ReadUInt(state, {index})",
            "long" => $"global::PopLua.Binding.Marshaller.ReadLong(state, {index})",
            "ulong" => $"global::PopLua.Binding.Marshaller.ReadULong(state, {index})",
            "float" => $"global::PopLua.Binding.Marshaller.ReadFloat(state, {index})",
            "double" => $"global::PopLua.Binding.Marshaller.ReadDouble(state, {index})",
            "string" => $"global::PopLua.Binding.Marshaller.ReadString(state, {index})",
            "Value" => $"global::PopLua.Binding.Marshaller.ReadValue(state, {index})",
            "FunctionRef" => $"global::PopLua.Binding.Marshaller.ReadFunctionRef(state, {index})",
            "ValueArray" => $"global::PopLua.Binding.Marshaller.ReadRest(state, {index})",
            "Descriptor" => $"{DescriptorReaderName((INamedTypeSymbol)type)}(state, {index}, {descriptorPathExpression ?? ("\"" + NameHelpers.Escape(DescriptorLuaName((INamedTypeSymbol)type)) + "\"")})",
            "DescriptorList" => $"{DescriptorListReaderName(DescriptorElementType(type)!)}(state, {index}, {descriptorPathExpression ?? "\"descriptor array\""})",
            "StringList" => $"global::PopLua.Binding.Marshaller.ReadStringList(state, {index}, {descriptorPathExpression ?? "\"string array\""})",
            "Userdata" => $"global::PopLua.Binding.Marshaller.ReadUserdata<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(state, {index}, {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.__PopLua_MetatableName)",
            _ => throw new InvalidOperationException("Unsupported type passed validation."),
        };

    private static string PushStatement(
        ITypeSymbol type,
        string expression,
        string depth = "0")
        => TypeKey(type) switch
        {
            "Userdata" =>
                $"global::PopLua.Binding.Marshaller.PushUserdata(state, {expression}, {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.__PopLua_MetatableName);",
            "Table" =>
                $"{TableWriterName((INamedTypeSymbol)type)}(state, {expression}, {depth});",
            "TableList" =>
                $"{TableListWriterName(TableElementType(type)!)}(state, {expression}, {depth});",
            "StringList" =>
                $"global::PopLua.Binding.Marshaller.PushStringList(state, {expression});",
            _ => $"global::PopLua.Binding.Marshaller.Push(state, {expression});",
        };

    private static string ValueExpression(string typeName, ValueMemberInfo value)
    {
        var member = typeName + "." + value.SymbolName;
        return TypeKey(value.Type) switch
        {
            "bool" => $"global::PopLua.Marshaling.Value.From({member})",
            "int" => $"global::PopLua.Marshaling.Value.From({member})",
            "uint" => $"global::PopLua.Marshaling.Value.From({member})",
            "long" => $"global::PopLua.Marshaling.Value.From({member})",
            "ulong" => $"global::PopLua.Marshaling.Value.From({member})",
            "float" => $"global::PopLua.Marshaling.Value.From({member})",
            "double" => $"global::PopLua.Marshaling.Value.From({member})",
            "string" => $"global::PopLua.Marshaling.Value.From({member})",
            "Value" => member,
            _ => throw new InvalidOperationException("Unsupported constant type passed validation."),
        };
    }

    private static bool IsSupportedParameter(ITypeSymbol type)
        => TypeKey(type) is "bool" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "string" or "Value" or "FunctionRef" or "ValueArray" or "Userdata" or "Descriptor" or "DescriptorList" or "StringList";

    private static bool IsSupportedValueMember(ITypeSymbol type)
        => TypeKey(type) is "bool" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "string" or "Value";

    private static bool IsSupportedReturn(ITypeSymbol type)
        => type.SpecialType == SpecialType.System_Void
            || TypeKey(type) is "bool" or "int" or "uint" or "long" or "ulong"
                or "float" or "double" or "string" or "Value" or "Userdata"
                or "ValueArray" or "Table" or "TableList" or "StringList";

    private static bool IsSupportedComputedPropertyReturn(ITypeSymbol type)
        => type.SpecialType != SpecialType.System_Void
            && IsSupportedReturn(type)
            && TypeKey(type) is not "ValueArray";

    private static bool TryGetValueTaskResult(ITypeSymbol type, out ITypeSymbol? resultType)
    {
        resultType = null;

        if (type is not INamedTypeSymbol namedType)
            return false;

        var name = namedType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (name == "global::System.Threading.Tasks.ValueTask")
            return true;

        if (name == "global::System.Threading.Tasks.ValueTask<TResult>" && namedType.TypeArguments.Length == 1)
        {
            resultType = namedType.TypeArguments[0];
            return true;
        }

        return false;
    }

    private static string TypeKey(ITypeSymbol type)
        => NullableValueType(type) is { } nullableValue
            ? TypeKey(nullableValue)
            : type.SpecialType switch
            {
                SpecialType.System_Boolean => "bool",
                SpecialType.System_Int32 => "int",
                SpecialType.System_UInt32 => "uint",
                SpecialType.System_Int64 => "long",
                SpecialType.System_UInt64 => "ulong",
                SpecialType.System_Single => "float",
                SpecialType.System_Double => "double",
                SpecialType.System_String => "string",
                _ when type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::PopLua.Marshaling.Value" => "Value",
                _ when type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::PopLua.Runtime.FunctionRef" => "FunctionRef",
                _ when IsValueArray(type) => "ValueArray",
                _ when IsUserdata(type) => "Userdata",
                _ when TableElementType(type) is not null => "TableList",
                _ when DescriptorElementType(type) is not null => "DescriptorList",
                _ when IsStringList(type) => "StringList",
                _ when IsTable(type) => "Table",
                _ when IsDescriptor(type) => "Descriptor",
                _ => "",
            };

    private static ITypeSymbol? NullableValueType(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && namedType.TypeArguments.Length == 1
            && namedType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Nullable<T>"
                ? namedType.TypeArguments[0]
                : null;

    private static bool IsUserdata(ITypeSymbol type)
        => type is INamedTypeSymbol namedType && HasAttribute(namedType, "PopLua.Binding.UserdataAttribute");

    private static bool IsTable(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && HasAttribute(namedType, "PopLua.Binding.TableAttribute");

    private static bool IsDescriptor(ITypeSymbol type)
        => type is INamedTypeSymbol namedType
            && namedType.TypeKind == TypeKind.Class
            && !namedType.IsAbstract
            && namedType.SpecialType == SpecialType.None
            && !IsTable(namedType)
            && !IsUserdata(namedType)
            && namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is not "global::PopLua.Runtime.FunctionRef"
            && !namedType.AllInterfaces.Any(i => i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Collections.IEnumerable")
            && namedType.InstanceConstructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0)
            && DescriptorProperties(namedType).Count > 0;

    private static INamedTypeSymbol? DescriptorElementType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType || namedType.TypeArguments.Length != 1)
            return null;

        var constructedFrom = namedType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (constructedFrom is not "global::System.Collections.Generic.IReadOnlyList<T>"
            and not "global::System.Collections.Generic.IList<T>"
            and not "global::System.Collections.Generic.List<T>")
        {
            return null;
        }

        return IsDescriptor(namedType.TypeArguments[0]) ? (INamedTypeSymbol)namedType.TypeArguments[0] : null;
    }

    private static INamedTypeSymbol? TableElementType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType || namedType.TypeArguments.Length != 1)
            return null;

        var constructedFrom = namedType.ConstructedFrom.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat);
        if (constructedFrom is not "global::System.Collections.Generic.IReadOnlyList<T>"
            and not "global::System.Collections.Generic.IList<T>"
            and not "global::System.Collections.Generic.List<T>")
        {
            return null;
        }

        return IsTable(namedType.TypeArguments[0])
            ? (INamedTypeSymbol)namedType.TypeArguments[0]
            : null;
    }

    private static IReadOnlyList<IPropertySymbol> DescriptorProperties(INamedTypeSymbol type)
        => type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public
                && !p.IsStatic
                && p.GetMethod is not null
                && p.SetMethod?.DeclaredAccessibility == Accessibility.Public
                && IsSupportedDescriptorMember(p.Type))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<IPropertySymbol> TableProperties(INamedTypeSymbol type)
        => type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(property => property.DeclaredAccessibility == Accessibility.Public
                && !property.IsStatic
                && property.GetMethod?.DeclaredAccessibility == Accessibility.Public
                && !HasAttribute(property, "PopLua.Binding.IgnoreAttribute")
                && IsSupportedTableMember(property.Type))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<IPropertySymbol> ContractProperties(INamedTypeSymbol type)
        => IsTable(type) ? TableProperties(type) : DescriptorProperties(type);

    private static bool IsSupportedDescriptorMember(ITypeSymbol type)
        => TypeKey(type) is "bool" or "int" or "uint" or "long" or "ulong" or "float" or "double" or "string" or "Value" or "Userdata" or "Descriptor" or "DescriptorList" or "StringList";

    private static bool IsSupportedTableMember(ITypeSymbol type)
        => TypeKey(type) is "bool" or "int" or "uint" or "long" or "ulong"
            or "float" or "double" or "string" or "Value" or "Userdata"
            or "Table" or "TableList" or "StringList";

    private static bool ValidateTableContracts(
        SourceProductionContext context,
        IEnumerable<ITypeSymbol> exposedTypes)
    {
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var valid = true;

        foreach (var type in exposedTypes)
            valid &= ValidateTableContract(context, type, seen);

        return valid;
    }

    private static bool ValidateTableContract(
        SourceProductionContext context,
        ITypeSymbol type,
        HashSet<ITypeSymbol> seen)
    {
        var table = TableElementType(type) ?? (IsTable(type) ? (INamedTypeSymbol)type : null);
        if (table is null || !seen.Add(table))
            return true;

        var valid = true;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in table.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.DeclaredAccessibility != Accessibility.Public
                || property.IsStatic
                || property.GetMethod?.DeclaredAccessibility != Accessibility.Public
                || HasAttribute(property, "PopLua.Binding.IgnoreAttribute"))
            {
                continue;
            }

            if (!IsSupportedTableMember(property.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedType,
                    property.Locations.FirstOrDefault(),
                    property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                valid = false;
                continue;
            }

            var fieldName = DescriptorFieldName(property);
            if (!names.Add(fieldName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.DuplicateLuaName,
                    property.Locations.FirstOrDefault(),
                    fieldName,
                    table.Name));
                valid = false;
            }

            valid &= ValidateTableContract(context, property.Type, seen);
        }

        return valid;
    }

    private static bool IsStringList(ITypeSymbol type)
        => type is INamedTypeSymbol named && named.TypeArguments.Length == 1
            && named.TypeArguments[0].SpecialType == SpecialType.System_String
            && named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                is "global::System.Collections.Generic.IReadOnlyList<T>"
                or "global::System.Collections.Generic.IList<T>"
                or "global::System.Collections.Generic.List<T>";

    private static bool IsRequired(IPropertySymbol property)
        => property.IsRequired
            || property.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.RequiredMemberAttribute");

    private static string DescriptorFieldName(IPropertySymbol property)
        => property.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "PopLua.Binding.FieldAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string
            ?? NameHelpers.ToSnakeCase(property.Name);

    private static string DescriptorLuaName(INamedTypeSymbol type)
        => NameHelpers.ToSnakeCase(type.Name);

    private static string DescriptorSafeName(INamedTypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace(".", "_")
            .Replace("+", "_")
            .Replace("<", "_")
            .Replace(">", "_");

    private static string DescriptorReaderName(INamedTypeSymbol type)
        => "__PopLua_ReadDescriptor_" + DescriptorSafeName(type);

    private static string DescriptorListReaderName(INamedTypeSymbol type)
        => "__PopLua_ReadDescriptorList_" + DescriptorSafeName(type);

    private static string TableWriterName(INamedTypeSymbol type)
        => "__PopLua_PushTable_" + DescriptorSafeName(type);

    private static string TableListWriterName(INamedTypeSymbol type)
        => "__PopLua_PushTableList_" + DescriptorSafeName(type);

    private static bool IsValueArray(ITypeSymbol type)
        => type is IArrayTypeSymbol array
            && array.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::PopLua.Marshaling.Value";

    private static bool IsStaticValueMember(ISymbol member)
        => member switch
        {
            IFieldSymbol field => field.IsStatic,
            IPropertySymbol property => property.IsStatic,
            _ => false,
        };

    private static bool IsWritableValueMember(ISymbol member)
        => member switch
        {
            IFieldSymbol field => !field.IsReadOnly && !field.IsConst && !field.IsStatic,
            IPropertySymbol property => property.SetMethod?.DeclaredAccessibility == Accessibility.Public && !property.IsStatic,
            _ => false,
        };

    private static string ValueKind(ISymbol member)
        => member switch
        {
            IFieldSymbol => "field",
            IPropertySymbol => "property",
            _ => "value",
        };

    private static bool ShouldGenerate(AnalyzerConfigOptionsProvider options, string propertyName)
    {
        if (!options.GlobalOptions.TryGetValue("build_property." + propertyName, out var value))
            return true;

        return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, "0", StringComparison.Ordinal);
    }

    private static void ReportUnsafeBlocksRequiredIfNeeded(
        SourceProductionContext context,
        Compilation compilation,
        TypeDeclarationSyntax syntax,
        bool allowUnsafe,
        ref bool reported)
    {
        if (allowUnsafe || reported)
            return;

        reported = true;
        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.UnsafeBlocksRequired,
            syntax.Identifier.GetLocation(),
            compilation.AssemblyName ?? "this project"));
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
        => GetAttribute(symbol, metadataName) is not null;

    private static AttributeData? GetAttribute(ISymbol symbol, string metadataName)
        => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == metadataName);

    private static string? GetConstructorString(AttributeData attribute)
        => attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;

    private static string? GetNamedString(AttributeData attribute, string name)
        => attribute.NamedArguments.FirstOrDefault(kv => kv.Key == name).Value.Value as string;

    private static bool GetNamedBool(AttributeData attribute, string name)
        => TryGetNamedBool(attribute, name, out var value) && value;

    private static bool GetNamedBool(AttributeData attribute, string name, bool defaultValue)
        => TryGetNamedBool(attribute, name, out var value) ? value : defaultValue;

    private static bool TryGetNamedBool(AttributeData attribute, string name, out bool value)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                value = argument.Value.Value is true;
                return true;
            }
        }

        foreach (var syntaxReference in attribute.ApplicationSyntaxReference is null
            ? []
            : new[] { attribute.ApplicationSyntaxReference })
        {
            if (syntaxReference.GetSyntax() is not AttributeSyntax syntax || syntax.ArgumentList is null)
                continue;

            foreach (var argument in syntax.ArgumentList.Arguments)
            {
                if (argument.NameEquals?.Name.Identifier.ValueText != name)
                    continue;

                if (argument.Expression.IsKind(SyntaxKind.TrueLiteralExpression))
                {
                    value = true;
                    return true;
                }

                if (argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression))
                {
                    value = false;
                    return true;
                }
            }
        }

        value = false;
        return false;
    }

    private static bool IsPartial(TypeDeclarationSyntax syntax)
        => syntax.Modifiers.Any(SyntaxKind.PartialKeyword);

    private static string GetHintName(INamedTypeSymbol type)
        => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace(".", "_")
            .Replace("+", "_");

    private sealed class ModuleInfo
    {
        public ModuleInfo(string hintName, string typeName, string source, ModuleModel model, IReadOnlyList<DescriptorModel> descriptors)
        {
            HintName = hintName;
            TypeName = typeName;
            Source = source;
            Model = model;
            Descriptors = descriptors;
        }

        public string HintName { get; }
        public string TypeName { get; }
        public string Source { get; }
        public ModuleModel Model { get; }
        public IReadOnlyList<DescriptorModel> Descriptors { get; }
    }

    private sealed class UserdataInfo
    {
        public UserdataInfo(string hintName, string source, UserdataModel model, IReadOnlyList<DescriptorModel> descriptors)
        {
            HintName = hintName;
            Source = source;
            Model = model;
            Descriptors = descriptors;
        }

        public string HintName { get; }
        public string Source { get; }
        public UserdataModel Model { get; }
        public IReadOnlyList<DescriptorModel> Descriptors { get; }
    }

    private sealed class MethodInfo
    {
        public MethodInfo(
            IMethodSymbol symbol,
            string symbolName,
            string luaName,
            ITypeSymbol returnType,
            ITypeSymbol? asyncResultType,
            IReadOnlyList<ParameterInfo> parameters,
            bool isStatic,
            bool isAsync,
            bool pauseTime)
        {
            Symbol = symbol;
            SymbolName = symbolName;
            LuaName = luaName;
            ReturnType = returnType;
            AsyncResultType = asyncResultType;
            Parameters = parameters;
            IsStatic = isStatic;
            IsAsync = isAsync;
            PauseTime = pauseTime;
        }

        public IMethodSymbol Symbol { get; }
        public string SymbolName { get; }
        public string LuaName { get; }
        public ITypeSymbol ReturnType { get; }
        public ITypeSymbol? AsyncResultType { get; }
        public IReadOnlyList<ParameterInfo> Parameters { get; }
        public bool IsStatic { get; }
        public bool IsAsync { get; }
        public bool PauseTime { get; }
    }

    private sealed class ParameterInfo
    {
        public ParameterInfo(IParameterSymbol symbol, string name, ITypeSymbol type, bool isContext)
        {
            Symbol = symbol;
            Name = name;
            Type = type;
            IsContext = isContext;
        }

        public IParameterSymbol Symbol { get; }
        public string Name { get; }
        public ITypeSymbol Type { get; }
        public bool IsContext { get; }
    }

    private sealed class ValueMemberInfo
    {
        public ValueMemberInfo(
            ISymbol symbol,
            string symbolName,
            string luaName,
            ITypeSymbol type,
            bool isWritable = false,
            string kind = "property")
        {
            Symbol = symbol;
            SymbolName = symbolName;
            LuaName = luaName;
            Type = type;
            IsWritable = isWritable;
            Kind = kind;
        }

        public ISymbol Symbol { get; }
        public string SymbolName { get; }
        public string LuaName { get; }
        public ITypeSymbol Type { get; }
        public bool IsWritable { get; }
        public string Kind { get; }
    }

    private sealed class ComputedPropertyInfo
    {
        public ComputedPropertyInfo(
            IMethodSymbol symbol,
            string symbolName,
            string luaName,
            ITypeSymbol returnType,
            IReadOnlyList<ParameterInfo> parameters,
            bool isStatic)
        {
            Symbol = symbol;
            SymbolName = symbolName;
            LuaName = luaName;
            ReturnType = returnType;
            Parameters = parameters;
            IsStatic = isStatic;
        }

        public IMethodSymbol Symbol { get; }
        public string SymbolName { get; }
        public string LuaName { get; }
        public ITypeSymbol ReturnType { get; }
        public IReadOnlyList<ParameterInfo> Parameters { get; }
        public bool IsStatic { get; }
    }

    private sealed class OperatorInfo
    {
        public OperatorInfo(IMethodSymbol symbol, string symbolName, string metamethod, ITypeSymbol returnType, IReadOnlyList<ITypeSymbol> parameters)
        {
            Symbol = symbol;
            SymbolName = symbolName;
            Metamethod = metamethod;
            ReturnType = returnType;
            Parameters = parameters;
        }

        public IMethodSymbol Symbol { get; }
        public string SymbolName { get; }
        public string Metamethod { get; }
        public ITypeSymbol ReturnType { get; }
        public IReadOnlyList<ITypeSymbol> Parameters { get; }
    }
}

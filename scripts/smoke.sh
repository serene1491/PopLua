#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

version="1.0.0-rc.1"
runtime_pkg="$repo_root/src/PopLua/bin/Release/PopLua.${version}.nupkg"
templates_pkg="$repo_root/templates/PopLua.Templates/bin/Release/PopLua.Templates.${version}.nupkg"

test -f "$runtime_pkg"
test -f "$templates_pkg"

work="$(mktemp -d /tmp/poplua-rc1-smoke.XXXXXX)"
trap 'rm -rf "$work"' EXIT

feed="$work/feed"
mkdir -p "$feed"
cp "$runtime_pkg" "$feed/"
cp "$templates_pkg" "$feed/"

cat > "$work/NuGet.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feed" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML

export DOTNET_CLI_HOME="$work/dotnet-home"
export NUGET_PACKAGES="$work/nuget-packages"

cd "$work"

dotnet new install "feed/PopLua.Templates.${version}.nupkg"
dotnet new poplua -n SmokeMinimal
dotnet new poplua-bindings -n SmokeBindings
dotnet new poplua-host -n SmokeHost

test -d SmokeMinimal
test -d SmokeBindings
test -d SmokeHost
test -f SmokeHost/SmokeHost.sln

dotnet build SmokeMinimal --configfile NuGet.config
dotnet run --project SmokeMinimal --no-restore
dotnet build SmokeBindings --configfile NuGet.config
dotnet build SmokeHost --configfile NuGet.config
dotnet run --project SmokeHost/SmokeHost.App --no-restore

mkdir RuntimeOnly GeneratedConsumer MissingUnsafe

cat > RuntimeOnly/RuntimeOnly.csproj <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="PopLua" Version="$version" />
  </ItemGroup>
</Project>
XML

cat > RuntimeOnly/Program.cs <<'CS'
using PopLua.Binding;
using PopLua.Context;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b.Require((ctx, name) =>
    name == "util" ? Chunk.Code("return { answer = 42 }", "module:util.lua") : null));

var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
await using var session = lua.Session(sandbox);

var result = await session.Run<long>(Chunk.Code("local util = require('util'); return math.max(util.answer, 41)", "consumer:main.lua"));
Console.WriteLine($"runtime={result.Unwrap()}");
CS

cat > GeneratedConsumer/GeneratedConsumer.csproj <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <PopLuaApiOutputDir>\$(MSBuildProjectDirectory)/artifacts/poplua-api</PopLuaApiOutputDir>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="PopLua" Version="$version" />
  </ItemGroup>
</Project>
XML

cat > GeneratedConsumer/Program.cs <<'CS'
using PopLua.Binding;
using PopLua.Context;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

var lua = Engine.Create(b => b
    .Module<HostModule>()
    .Require((ctx, name) => name == "util" ? Chunk.Code("return { tag = 'ok' }", "module:util.lua") : null));

var sandbox = Sandbox.Build(b => b.AllowSafeLibs());
await using var session = lua.Session(sandbox);

var result = await session.Run<string>(Chunk.Code("""
    local util = require("util")
    local box = host.box("Serene")
    return util.tag .. ":" ..
        host.add(1, 2) .. ":" ..
        host.add_async(20, 22) .. ":" ..
        box:name() .. ":" ..
        box:name_async() .. ":" ..
        host.answer
    """, "consumer:generated.lua"));

Console.WriteLine(result.Unwrap());

[Module("host")]
public partial class HostModule
{
    [Const("answer")]
    public const long Answer = 42;

    [Fn("add")]
    public static long Add(long left, long right) => left + right;

    [Fn("add_async", Async = true)]
    public static ValueTask<long> AddAsync(long left, long right) => ValueTask.FromResult(left + right);

    [Fn("box")]
    public static Box Box(string name) => new(name);
}

[Userdata("box")]
public partial class Box(string name)
{
    [Fn("name")]
    public string Name() => name;

    [Fn("name_async", Async = true)]
    public ValueTask<string> NameAsync() => ValueTask.FromResult("async-" + name);
}
CS

cat > MissingUnsafe/MissingUnsafe.csproj <<XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="PopLua" Version="$version" />
  </ItemGroup>
</Project>
XML

cat > MissingUnsafe/Program.cs <<'CS'
using PopLua.Binding;
using PopLua.Context;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

[Module("bad")]
public partial class BadModule
{
    [Fn("value")]
    public static long Value() => 1;
}
CS

dotnet build RuntimeOnly --configfile NuGet.config
dotnet run --project RuntimeOnly --no-restore
dotnet build GeneratedConsumer --configfile NuGet.config
dotnet run --project GeneratedConsumer --no-restore
python3 -m json.tool GeneratedConsumer/artifacts/poplua-api/poplua.api.json >/dev/null
grep -R "name_async" GeneratedConsumer/artifacts/poplua-api >/dev/null

dotnet publish RuntimeOnly \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained \
    -p:PublishAot=true \
    --configfile NuGet.config
dotnet publish GeneratedConsumer \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained \
    -p:PublishAot=true \
    --configfile NuGet.config
RuntimeOnly/bin/Release/net10.0/linux-x64/publish/RuntimeOnly
GeneratedConsumer/bin/Release/net10.0/linux-x64/publish/GeneratedConsumer

if dotnet build MissingUnsafe --configfile NuGet.config > missing-unsafe.log 2>&1; then
    echo "MissingUnsafe unexpectedly built successfully." >&2
    exit 1
fi

grep "PLUA010" missing-unsafe.log >/dev/null

echo "Package smoke validation passed."

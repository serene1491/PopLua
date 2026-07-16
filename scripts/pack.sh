#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

version="1.0.0-rc.1"
runtime_pkg="src/PopLua/bin/Release/PopLua.${version}.nupkg"
templates_pkg="templates/PopLua.Templates/bin/Release/PopLua.Templates.${version}.nupkg"

dotnet build PopLua.sln
dotnet test PopLua.sln --no-build
dotnet pack src/PopLua/PopLua.csproj -c Release
dotnet pack templates/PopLua.Templates/PopLua.Templates.csproj -c Release

test -f "$runtime_pkg"
test -f "$templates_pkg"

unexpected_packages="$(
    find src/PopLua/bin/Release templates/PopLua.Templates/bin/Release \
        -maxdepth 1 -name '*.nupkg' \
        ! -name "PopLua.${version}.nupkg" \
        ! -name "PopLua.Templates.${version}.nupkg" \
        -print
)"

if [[ -n "$unexpected_packages" ]]; then
    echo "Unexpected package artifacts:" >&2
    echo "$unexpected_packages" >&2
    exit 1
fi

python3 - "$runtime_pkg" "$templates_pkg" <<'PY'
import sys
import zipfile

runtime_pkg, templates_pkg = sys.argv[1:]

def names(path):
    with zipfile.ZipFile(path) as z:
        return sorted(z.namelist())

runtime = names(runtime_pkg)
templates = names(templates_pkg)

required_runtime = {
    "PopLua.nuspec",
    "PACKAGE_README.md",
    "analyzers/dotnet/cs/PopLua.Generators.dll",
    "buildTransitive/PopLua.props",
    "buildTransitive/PopLua.targets",
    "lib/net10.0/PopLua.dll",
    "lib/net10.0/PopLua.xml",
    "poplua.png",
}

for item in required_runtime:
    if item not in runtime:
        raise SystemExit(f"runtime package missing {item}")

bad_runtime_markers = (
    "content/",
    "examples/",
    "templates/",
    "BenchmarkDotNet.Artifacts/",
    "tests/",
    "smoke/",
)
bad_runtime_suffixes = (".log", ".svg")

for item in runtime:
    if item.endswith(bad_runtime_suffixes) or any(marker in item for marker in bad_runtime_markers):
        raise SystemExit(f"unexpected runtime package entry: {item}")
    if "native" in item.lower() or "lua5.4" in item.lower() or "lua5.5" in item.lower():
        raise SystemExit(f"unexpected native payload in runtime package: {item}")

if "PopLua.Templates.nuspec" not in templates or "README.md" not in templates:
    raise SystemExit("template package missing nuspec or README")
if not any(item == "content/poplua/.template.config/template.json" for item in templates):
    raise SystemExit("template package missing poplua template")
if not any(item == "content/poplua-host/PopLuaHost.sln" for item in templates):
    raise SystemExit("template package missing host solution")

bad_template_markers = (
    "BenchmarkDotNet.Artifacts/",
    "examples/",
    "tests/",
    "smoke/",
    "bin/Debug/",
    "bin/Release/",
    "obj/",
)

for item in templates:
    if item.endswith(".log") or any(marker in item for marker in bad_template_markers):
        raise SystemExit(f"unexpected template package entry: {item}")
    if (
        item.startswith("lib/")
        or "native" in item.lower()
        or "lua5.4" in item.lower()
        or "lua5.5" in item.lower()
    ):
        raise SystemExit(f"unexpected payload in template package: {item}")

print("Package contents look correct.")
PY

echo "Pack validation passed for ${version}."

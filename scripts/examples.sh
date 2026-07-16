#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

while IFS= read -r project; do
    echo "Building ${project}"
    dotnet build "$project" --nologo
done < <(find examples -name '*.csproj' | sort)

key_examples=(
    "examples/Minimal.csproj"
    "examples/Basics.csproj"
    "examples/SafeLibs.csproj"
    "examples/ControlledRequire.csproj"
    "examples/ContextModule.csproj"
    "examples/Descriptors.csproj"
    "examples/Userdata.csproj"
    "examples/Callbacks.csproj"
    "examples/Async.csproj"
    "examples/AsyncUserdata.csproj"
    "examples/Diagnostics.csproj"
    "examples/SplitHost/SplitHost.App/SplitHost.App.csproj"
)

for project in "${key_examples[@]}"; do
    echo "--- ${project}"
    dotnet run --project "$project"
done

while IFS= read -r manifest; do
    echo "Validating ${manifest}"
    python3 -m json.tool "$manifest" >/dev/null
done < <(find examples/obj -path '*/poplua-api/poplua.api.json' -type f | sort)

echo "Examples validation passed."

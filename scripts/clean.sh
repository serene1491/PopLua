#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

rm -rf BenchmarkDotNet.Artifacts
find . -type d -name BenchmarkDotNet.Artifacts -prune -exec rm -rf {} +
rm -f BenchmarkRun-*.log
rm -f src/PopLua/bin/Release/*.nupkg
rm -f templates/PopLua.Templates/bin/Release/*.nupkg

echo "Cleaned generated validation artifacts."

#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

./scripts/clean.sh
./scripts/pack.sh
./scripts/examples.sh
./scripts/smoke.sh
git diff --check

echo "Release validation passed for 1.0.0-rc.1."
echo "Do not publish automatically."

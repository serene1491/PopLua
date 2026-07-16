# Release Guide

This guide is for maintainers preparing a PopLua preview or stable package.
Publishing is intentionally manual.

## Before A Release

1. Update package versions in the runtime package, template package, templates,
   docs, scripts, and tests that assert the current version.
2. Update `CHANGELOG.md`.
3. Keep package release notes short in the project files. Detailed history
   belongs in `CHANGELOG.md`.
4. Review `docs/maturity.md` and `docs/roadmap.md` so completed work is not
   still listed as future work.
5. Run the full validation script:

```bash
./scripts/check.sh
```

The script builds, tests, packs both packages, validates package contents,
builds and runs examples, installs templates from a local package, runs clean
package consumers, and checks whitespace with `git diff --check`.

## Package Inspection

The runtime package should contain only:

- runtime DLL and XML docs;
- generator analyzer;
- `buildTransitive` props/targets;
- package readme;
- package icon;
- NuGet metadata.

The runtime package should not contain examples, templates, benchmark artifacts,
native Lua binaries, smoke projects, logs, test outputs, or repository scratch
files.

The template package should contain template content and template documentation
only.

## Manual Publish

After `./scripts/check.sh` passes and package contents are reviewed, publishing
can be done manually by a maintainer with the appropriate NuGet credentials.

Do not add `dotnet nuget push` to repository validation scripts.

## After A Release

1. Confirm the package appears on NuGet.org.
2. Install from NuGet.org in a clean project if practical.
3. Move completed release work out of future roadmap sections.
4. Start the next changelog section with an empty unreleased state.

# AGENTS.md

## Project

PopLua is an existing .NET 10 Lua 5.4 or 5.5 runtime for C#.

It provides source-generated module/userdata bindings, async module and userdata
methods, sandboxing, quotas, diagnostics, controlled host-managed `require`, safe
Lua library profiles, templates, examples, and generated API artifacts.

## Before Changing Code

Read the smallest relevant docs before editing.

For runtime or public API changes, check:

* `espec.md`
* `docs/technical-reference.md`
* `docs/roadmap.md`
* `CHANGELOG.md`

For async, coroutine, cancellation, or scheduler work, also check:

* `docs/async-coroutine-bridge-design.md`

For sandbox, host APIs, script author behavior, or `require`, also check:

* `docs/script-author-guide.md`
* `examples/README.md`
* `examples/SplitHost/README.md`

For packaging, templates, or release validation, also check:

* `src/PopLua/PACKAGE_README.md`
* `templates/PopLua.Templates/README.md`
* `README.md`

Do not assume old behavior from memory. If docs and code disagree, identify the
disagreement before changing behavior.

## Work Rules

* Prefer small, correct changes over broad rewrites.
* Do not add placeholders or fake completed code.
* Do not publish packages from agent tasks.
* Keep native Lua interop internal.
* Keep binding generation source-generated and AOT-friendly.
* Preserve sandbox, quota, diagnostics, async, and userdata lifetime semantics.
* Add or update tests with behavior changes.
* Measure before optimizing.
* Keep optimizations narrow and benchmark-backed.
* Do not commit generated benchmark artifacts.
* Keep package release notes short; put details in `CHANGELOG.md`.

## API and Behavior Rules

* Runtime-only consumers should not need `AllowUnsafeBlocks`.
* Generated binding projects may require `AllowUnsafeBlocks`.
* Beginner examples should prefer `Sandbox.Untrusted` plus explicit opt-ins.
* Do not use `Sandbox.Trusted` just to make an example work.
* Do not imply PopLua performs filesystem or package-search loading for `require`.
* Host-managed `require` should stay explicit and controlled.
* Script errors should flow through `Result`; host misuse may throw.
* Do not expose implementation details as public API for convenience.

## Validation

Use repository scripts when available:

```bash
./scripts/check.sh
```

For focused work, use the smallest relevant command:

```bash
dotnet build PopLua.sln
dotnet test PopLua.sln --no-build
./scripts/examples.sh
./scripts/smoke.sh
```

After package, template, or release-related changes, run:

```bash
./scripts/check.sh
```

## Docs

* Roadmap records committed release scope; speculative ideas belong in issues.
* Changelog is for completed work.
* Package release notes should be short.
* XML docs should help IntelliSense without becoming tutorials.
* Design docs may explain why a design exists, but should not describe
  implemented features as only planned.
* Update docs when behavior, public API, examples, templates, or package flow changes.

## Packages

Runtime package should contain:

* runtime DLL/XML;
* generator analyzer;
* buildTransitive files;
* package readme;
* icon;
* NuGet metadata.

Template package should contain template content and readmes only.

Do not include examples, benchmark artifacts, smoke projects, native binaries,
logs, or scratch files in either package.

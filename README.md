# tuf-dotnet

A .NET implementation of parts of The Update Framework (TUF) and utilities for canonical JSON handling.

This repository contains two libraries and tests used by the project:

- `CanonicalJson` — utilities for producing and parsing canonical JSON.
- `tuf-dotnet` — models and serialization helpers for TUF metadata and signing.
- Test projects for both libraries under `*.Tests` directories.

## Quick start

Prerequisites

- .NET SDK 10.0 or newer (needed for SLNX - the library targets .NET8+)

Build (from repository root)

```shell
dotnet build
```

Run tests

```shell
dotnet test
```

Or run tests for a single project:

```shell
dotnet test CanonicalJson.Tests/CanonicalJson.Tests.csproj
```

## Project layout

Root solution: `tuf-dotnet.slnx`

- `CanonicalJson/` — Canonical JSON library and its tests.
- `tuf-dotnet/` — TUF models, signing, and serialization code.

## Contributing

1. Fork the repository and create a feature branch.
2. Add tests for new behavior or bug fixes.
3. Run `dotnet build` and `dotnet test` locally.
4. Open a pull request with a concise description of changes.

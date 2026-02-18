# Contributing to Ouroboros App

Thanks for your interest in contributing! This guide covers the essentials for getting a productive development environment set up and submitting changes.

## Prerequisites

| Tool | Required | Notes |
|------|----------|-------|
| .NET 10.0 SDK | Yes | `dotnet --version` to verify |
| Git (with submodule support) | Yes | |
| Ollama | No | Needed for local LLM testing |
| Docker / Docker Compose | No | Needed for full stack (`docker-compose.yml`) |
| MAUI workload | No | Only for Android builds (`dotnet workload install maui-android`) |

## First-Time Setup

```bash
# 1. Clone with submodules
git clone --recurse-submodules https://github.com/PMeeske/ouroboros-app.git
cd ouroboros-app

# 2. Copy env template and fill in values you need
cp .env.example .env

# 3. Restore and build
dotnet restore
dotnet build

# 4. Run the doctor to verify your environment
cd src/Ouroboros.CLI
dotnet run -- doctor
```

## Project Structure

```
src/
  Ouroboros.Application/   Shared library (multi-target)
  Ouroboros.CLI/           Console app
  Ouroboros.WebApi/        ASP.NET Core REST API
  Ouroboros.Android/       MAUI mobile app
  Ouroboros.Easy/          Simplified API
  Ouroboros.Examples/      Sample code

tests/
  Ouroboros.Application.Tests/
  Ouroboros.CLI.Tests/
  Ouroboros.WebApi.Tests/
  Ouroboros.Android.Tests/
  Ouroboros.App.BDD/
  Ouroboros.Integration.Tests/
```

## Running Tests

```bash
# All tests (except Android / integration)
dotnet test

# Specific project
dotnet test tests/Ouroboros.CLI.Tests
dotnet test tests/Ouroboros.WebApi.Tests

# Integration tests (may need API keys)
dotnet test tests/Ouroboros.Integration.Tests --filter "Category!=RequiresApiKey"

# BDD / SpecFlow
dotnet test tests/Ouroboros.App.BDD
```

## Code Style

- An `.editorconfig` is provided at the repo root — your IDE should pick it up automatically.
- SonarAnalyzer.CSharp is enabled on all projects for static analysis.
- Use file-scoped namespaces, nullable reference types, and C# 14.0 features.
- Follow existing naming conventions (`_camelCase` for private fields, `PascalCase` for public members).

## Running the Stack Locally

```bash
# Start Ollama + Qdrant + WebAPI
docker compose up -d

# Or start only infrastructure (Ollama + Qdrant) and run WebAPI from source
docker compose up -d ollama qdrant
cd src/Ouroboros.WebApi && dotnet run

# Include Jaeger for tracing
docker compose --profile observability up -d
```

## Submitting Changes

1. Create a feature branch from `main`.
2. Make your changes, keeping commits focused and well-described.
3. Ensure all tests pass: `dotnet test`.
4. Open a pull request against `main`.

### PR Checklist

- [ ] All existing tests pass
- [ ] New features include tests
- [ ] No secrets or credentials committed
- [ ] Documentation updated if applicable

## Architecture Notes

- **Dependency Injection** is used throughout (`Microsoft.Extensions.DependencyInjection`).
- **System.CommandLine 2.x** powers the CLI — add new commands in `src/Ouroboros.CLI/Commands/`.
- **Minimal APIs** are used in the WebAPI — endpoints are defined in `Program.cs`.
- Cross-layer references (Foundation, Engine) are resolved via `Directory.Build.props` MSBuild properties.
- Git submodules under `libs/` provide Foundation, Engine, and Build dependencies.

## Getting Help

- Check `docs/TROUBLESHOOTING.md` for common issues.
- Run `dotnet run -- doctor` from the CLI project to diagnose environment problems.
- Open an issue on the [Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2) repository.

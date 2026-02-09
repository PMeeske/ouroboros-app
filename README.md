# Ouroboros App Layer

[![CI](https://github.com/PMeeske/ouroboros-app/actions/workflows/ci.yml/badge.svg)](https://github.com/PMeeske/ouroboros-app/actions/workflows/ci.yml)
[![Mutation Testing](https://github.com/PMeeske/ouroboros-app/actions/workflows/mutation.yml/badge.svg)](https://github.com/PMeeske/ouroboros-app/actions/workflows/mutation.yml)

Application layer of the Ouroboros system, providing user-facing interfaces and remoting capabilities for the Ouroboros AI pipeline framework.

This repository is part of the [Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2) ecosystem and contains the application implementations that sit on top of the foundation and engine layers.

## Overview

The Ouroboros App layer provides multiple interfaces for interacting with the Ouroboros system:
- **Android App** - Mobile terminal-style interface with AI provider support
- **Web API** - REST API for containerized/cloud deployments
- **CLI** - Command-line interface for desktop usage
- **Easy API** - Simplified programming interface
- **Examples** - Sample implementations and usage patterns

## Projects

### Applications

#### 🤖 [Ouroboros.Android](src/Ouroboros.Android/)
Native Android application providing a terminal-style CLI interface on mobile devices.
- Full Ollama API integration with authentication
- Multi-provider AI support (OpenAI, Anthropic, Google AI, etc.)
- Symbolic reasoning engine
- Command history and intelligent suggestions
- Offline-capable with local storage

#### 🌐 [Ouroboros.WebApi](src/Ouroboros.WebApi/)
ASP.NET Core Web API providing REST endpoints for the Ouroboros pipeline.
- Kubernetes-ready with health checks
- OpenAPI/Swagger documentation
- Horizontal scaling support
- Distributed tracing integration

#### 💻 Ouroboros.CLI
Command-line interface for desktop environments.
- Direct access to pipeline functionality
- Shell integration
- Batch processing support

### Libraries

#### 🎯 Ouroboros.Application
Shared pipeline runtime and application logic used by all application interfaces.

#### 🚀 Ouroboros.Easy
Simplified API for quick integration and prototyping.

#### 📚 Ouroboros.Examples
Sample implementations demonstrating Ouroboros capabilities.

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- For Android: MAUI workload (`dotnet workload install maui-android`)
- For Docker deployments: Docker and Docker Compose

### Building

#### Build All Projects (except Android)

```bash
dotnet restore
dotnet build
```

#### Build Android App

The Android app is built separately to avoid requiring MAUI workloads in all environments:

```bash
cd src/Ouroboros.Android
dotnet restore
dotnet build -c Release -f net10.0-android
```

The APK will be at: `bin/Release/net10.0-android/com.adaptivesystems.Ouroboros-Signed.apk`

See the [Android README](src/Ouroboros.Android/README.md) for detailed build and installation instructions.

#### Build Web API with Docker

```bash
docker build -f Dockerfile.webapi -t ouroboros-webapi:latest .
docker run -p 8080:8080 ouroboros-webapi:latest
```

### Running Tests

```bash
dotnet test
```

Run specific test projects:

```bash
dotnet test tests/Ouroboros.Application.Tests
dotnet test tests/Ouroboros.WebApi.Tests
dotnet test tests/Ouroboros.CLI.Tests
```

## Architecture

This repository represents the **App Layer** of the Ouroboros architecture:

```
┌─────────────────────────────────────────┐
│         Ouroboros App Layer             │
│  (Android, WebApi, CLI, Easy, Examples) │
├─────────────────────────────────────────┤
│       Ouroboros Engine Layer            │
│    (Pipeline, Providers, Network)       │
├─────────────────────────────────────────┤
│     Ouroboros Foundation Layer          │
│      (Core, Domain, Tools)              │
└─────────────────────────────────────────┘
```

### Dependencies

The App layer depends on:
- **Foundation Layer** - Core types, domain models, and utilities
- **Engine Layer** - Pipeline execution, AI providers, networking

These dependencies are managed through the `Directory.Build.props` file and expect the foundation and engine layers to be available as submodules or in sibling directories.

## Documentation

- **[Android Testing Guide](docs/ANDROID_TESTING_GUIDE.md)** - Comprehensive guide for testing the Android app
- **[Deployment Guide](docs/DEPLOYMENT.md)** - Kubernetes and cloud deployment instructions
- **[Configuration Guide](docs/CONFIGURATION_AND_SECURITY.md)** - Configuration and security details
- **[Quick Start](docs/QUICKSTART.md)** - Quick start guide

Additional documentation is available in the `docs/` directory.

## Related Repositories

- **[Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2)** - Main repository with overall system architecture
- **Ouroboros Foundation** - Core framework and domain models (referenced via build system)
- **Ouroboros Engine** - Pipeline execution and AI provider integrations (referenced via build system)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please ensure:
- All tests pass before submitting PRs
- New features include appropriate tests
- Documentation is updated to reflect changes
- Code follows existing patterns and conventions

## Support

For issues, questions, or contributions, please visit the main [Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2) repository.

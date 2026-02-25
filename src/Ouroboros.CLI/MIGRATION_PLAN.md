# Ouroboros CLI Migration Plan

## Overview
This document outlines the incremental migration plan to refactor the existing CLI application to use modern .NET 10 patterns including System.CommandLine, Microsoft.Extensions.Hosting, Dependency Injection, and Spectre.Console.

## Target Architecture
Every command follows:
**Program.cs → Options → Config → Handler → Service Interface → Service Implementation**

## Migration Requirements

### Step 1: Introduce Host.CreateDefaultBuilder as Composition Root
- [x] Create `Program.cs` with HostBuilder
- [x] Create service registration extensions
- [x] Register existing services in DI container
- [x] Test basic host startup

### Step 2: Replace Manual Argument Parsing with System.CommandLine
- [x] Create RootCommand structure
- [x] Add subcommands for existing verbs
- [x] Create command handlers
- [x] **Migrate existing option classes** — COMPLETED
- [x] Test command parsing

### Step 3: Replace Console Output with Spectre.Console
- [x] Create SpectreConsoleService wrapper
- [x] Add Write(IRenderable) to ISpectreConsoleService for rich widget rendering
- [x] Update command handlers to use Spectre.Console (all handlers use ISpectreConsoleService)
- [x] Fix QualityCommandHandler IAnsiConsole escape hatch — refactored to use ISpectreConsoleService directly
- [ ] Replace Console.WriteLine calls in OuroborosAgent partial classes (~75 calls)
- [ ] Replace Console.WriteLine calls in subsystem files (~180 calls)
- [ ] Replace Console.WriteLine calls in legacy command files (~500+ calls)
- [ ] Replace Console.WriteLine calls in MediatR handlers (~45 calls)
- [ ] Replace Console.WriteLine calls in Setup/ files (~150 calls)

### Step 4: Add Global "--voice" Option Integration
- [x] Create VoiceIntegrationService
- [x] Add global voice option
- [x] Implement speech recognition flow
- [x] Unify voice services — single VoiceModeService (V2 deleted)
- [x] Align persona default to "Iaret" across all configs
- [x] Align LocalTts default to false (prefer cloud TTS) across all configs
- [x] Remove unused voice parameters (CognitivePhysics globalVoiceOption)
- [x] Document Room mode always-listening voice intent
- [ ] Test voice command handling
- [ ] Add cancellation support

### Step 5: Architectural Refactoring — COMPLETED
- [x] Extract shared options into composable groups (SharedOptions.cs)
- [x] Create dedicated command handlers for all commands
- [x] Simplify Program.cs entry point (549 → 151 lines)
- [x] Add BindConfig to OuroborosCommandOptions (eliminates 100-line inline extraction)
- [x] Fix thread-unsafe Console.SetOut with SemaphoreSlim
- [x] Update service registration for all handlers
- [x] Update tests for new composed option structure

### Step 6: MeTTa Refactoring — COMPLETED
- [x] Create MeTTaConfig immutable record (replaces legacy MeTTaOptions)
- [x] Create IMeTTaService interface and MeTTaService implementation
- [x] Refactor MeTTaCommandHandler to implement ICommandHandler<MeTTaConfig>
- [x] Move MeTTaCommandHandler to Ouroboros.CLI.Commands.Handlers namespace
- [x] Register IMeTTaService/MeTTaService in ServiceCollectionExtensions
- [x] Update MediatR RunMeTTaRequest/Handler to use MeTTaConfig
- [x] Update OuroborosAgent.Commands.cs to use IMeTTaService
- [x] Delete static MeTTaCommands.cs

### Step 7: Dead Code Cleanup — COMPLETED
- [x] Delete 5 unreachable command classes (BenchmarkCommands, DistinctionCommands, DreamCommands, SelfCommands, TestCommands)
- [x] Delete 17 orphaned Options files (all Distinction*, Benchmark, Dream, Self, Test, Explain, ListTokens, Skills, MeTTa, Ask, VoiceOptionsBase)
- [x] Remove unused AddAskCommandHandler() and AddMeTTaCommandHandler() DI helpers
- [x] Delete VoiceModeServiceV2 and VoiceModeConfigV2
- [x] Remove VoiceV2 flag from OuroborosConfig, OuroborosCommandOptions, AgentBootstrapper

## Architecture

### Composable Option Groups (SharedOptions.cs)
Commands compose the option groups they need, eliminating cross-command duplication:

| Group | Options | Used By |
|-------|---------|---------|
| `ModelOptions` | model, temperature, max-tokens, timeout, stream | ask, pipeline, orchestrator, skills |
| `EndpointOptions` | endpoint, api-key, endpoint-type | ask, pipeline, orchestrator, skills |
| `MultiModelOptions` | router, coder-model, summarize-model, reason-model, general-model | ask, pipeline, orchestrator |
| `DiagnosticOptions` | debug, strict-model, json-tools | ask, pipeline, orchestrator, skills |
| `EmbeddingOptions` | embed-model, qdrant | ask, skills |
| `CollectiveOptions` | collective, master-model, election-strategy, decompose, ... | ask, orchestrator |
| `AgentLoopOptions` | agent, agent-mode, agent-max-steps | ask, pipeline |
| `CommandVoiceOptions` | voice-only, local-tts, voice-loop | ask |

### Command Handler Pattern
Each command has a dedicated handler class in `Commands/Handlers/`:
- `AskCommandHandler` — question/answer with LLM
- `PipelineCommandHandler` — DSL pipeline execution
- `OuroborosCommandHandler` — unified agent lifecycle
- `ImmersiveCommandHandler` — immersive persona mode
- `RoomCommandHandler` — ambient room presence
- `SkillsCommandHandler` — skill listing and research
- `OrchestratorCommandHandler` — multi-model orchestration
- `CognitivePhysicsCommandHandler` — ZeroShift, trajectory, entangle, chaos
- `QualityCommandHandler` — product quality dashboard
- `MeTTaCommandHandler` — symbolic reasoning orchestrator

### Service Layer
Each handler delegates to a corresponding service:
- `IAskService` / `AskService`
- `IPipelineService` / `PipelineService`
- `IOuroborosAgentService` / `OuroborosAgentService`
- `IImmersiveModeService` / `ImmersiveModeService`
- `IRoomModeService` / `RoomModeService`
- `ISkillsService` / `SkillsService`
- `IOrchestratorService` / `OrchestratorService`
- `ICognitivePhysicsService` / `CognitivePhysicsService`
- `IMeTTaService` / `MeTTaService`

### Config Binding
`OuroborosCommandOptions.BindConfig(ParseResult)` maps all 60+ parsed CLI values
into a single `OuroborosConfig` record, including environment-variable fallbacks
and derived logic (Azure TTS key resolution, push-mode voice derivation, etc.).

## Remaining Work

### Legacy Commands Still Using Old Pattern
These command classes are called from OuroborosAgent's MediatR command routing
and still use the old Options classes + static methods pattern:
- AffectCommands → AffectOptions
- DagCommands → DagOptions
- EnvironmentCommands → EnvironmentOptions
- MaintenanceCommands → MaintenanceOptions
- NetworkCommands → NetworkOptions
- PolicyCommands → PolicyOptions
- PipelineCommands → PipelineOptions (also called directly from OuroborosAgent)
- OrchestratorCommands → OrchestratorOptions

Each needs: create Config record, create IXService/XService, refactor handler,
update MediatR request, delete old command class and Options file.

### Console.WriteLine Migration
~1600 Console.* calls remain across 52 files. Key targets:
- OuroborosAgent partial classes (RunLoop, Init, Voice, Cognition)
- 11 Subsystem files
- Setup/ files (OuroborosCliIntegration, GuidedSetup, AgentBootstrapper)
- Legacy command classes (once refactored to services)
- MediatR handlers

### Dependency Cleanup
- CommandLineParser NuGet — remove once all 13 remaining Options files are deleted
- `using Ouroboros.Options;` imports — clean up once Options folder is empty

## Migration Completion Status: 75%

### Completed:
- HostBuilder and DI setup
- System.CommandLine 2.0.3 GA integration
- Composable shared option groups (SharedOptions.cs)
- 10 dedicated command handlers (ask, pipeline, ouroboros, immersive, room, skills, orchestrator, cognitive-physics, quality, metta)
- 9 service interfaces with implementations
- OuroborosCommandOptions.BindConfig() for clean config mapping
- Thread-safe Console.SetOut with SemaphoreSlim in services
- Simplified Program.cs
- Voice integration service + unified VoiceModeService
- Spectre.Console service wrapper with Write(IRenderable)
- MeTTa fully refactored to handler → service pattern
- Dead code cleanup (5 command classes, 17 Options files, V2 voice)
- Consistent defaults (LocalTts=false, Persona="Iaret")
- **Build succeeds with 0 errors, 0 warnings**

### Remaining:
- Refactor 8 legacy command classes to handler → service pattern
- Replace ~1600 Console.WriteLine calls with ISpectreConsoleService
- Remove remaining 13 legacy Options files
- Remove CommandLineParser NuGet dependency
- CI/CD pipeline updates

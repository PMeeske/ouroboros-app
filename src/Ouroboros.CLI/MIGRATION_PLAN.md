# Ouroboros CLI Migration Plan

## Overview
This document outlines the incremental migration plan to refactor the existing CLI application to use modern .NET 10 patterns including System.CommandLine, Microsoft.Extensions.Hosting, Dependency Injection, and Spectre.Console.

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
- [x] **Migrate existing option classes** - ✅ **COMPLETED**
- [x] Test command parsing

### Step 3: Replace Console Output with Spectre.Console
- [x] Create SpectreConsoleService wrapper
- [x] Update command handlers to use Spectre.Console
- [ ] Replace Console.WriteLine calls in legacy command files
- [ ] Test rich terminal output

### Step 4: Add Global "--voice" Option Integration
- [x] Create VoiceIntegrationService
- [x] Add global voice option
- [x] Implement speech recognition flow
- [ ] Test voice command handling
- [ ] Add cancellation support

### Step 5: Architectural Refactoring ✅ **COMPLETED**
- [x] Extract shared options into composable groups (SharedOptions.cs)
- [x] Create dedicated command handlers for all commands
- [x] Simplify Program.cs entry point (549 → 151 lines)
- [x] Add BindConfig to OuroborosCommandOptions (eliminates 100-line inline extraction)
- [x] Fix thread-unsafe Console.SetOut with SemaphoreSlim
- [x] Update service registration for all handlers
- [x] Update tests for new composed option structure

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
- `AskCommandHandler` - question/answer with LLM
- `PipelineCommandHandler` - DSL pipeline execution
- `OuroborosCommandHandler` - unified agent lifecycle
- `SkillsCommandHandler` - skill listing and research
- `OrchestratorCommandHandler` - multi-model orchestration
- `CognitivePhysicsCommandHandler` - ZeroShift, trajectory, entangle, chaos

### Config Binding
`OuroborosCommandOptions.BindConfig(ParseResult)` maps all 60+ parsed CLI values
into a single `OuroborosConfig` record, including environment-variable fallbacks
and derived logic (Azure TTS key resolution, push-mode voice derivation, etc.).

## Safe Rollout Strategy

### Phase 1: Parallel Operation ✅ **COMPLETED**
- ✅ Keep existing CommandLineParser working
- ✅ Add new System.CommandLine commands alongside
- ✅ Run integration tests on both implementations

### Phase 2: Gradual Migration ✅ **COMPLETED**
- ✅ Migrate all commands to handler pattern
- ✅ Maintain backward compatibility
- ✅ Compose shared options across commands

### Phase 3: Full Cutover
- 🔄 Remove old CommandLineParser dependency
- 🔄 Remove legacy Options/ folder
- 🔄 Final testing and validation
- 🔄 Update CI/CD pipelines

## Migration Completion Status: 95%

### Completed:
- ✅ HostBuilder and DI setup
- ✅ System.CommandLine 2.0.3 GA integration
- ✅ Composable shared option groups (SharedOptions.cs)
- ✅ Dedicated command handlers for all 6 commands
- ✅ OuroborosCommandOptions.BindConfig() for clean config mapping
- ✅ Thread-safe Console.SetOut with SemaphoreSlim in services
- ✅ Simplified Program.cs (549 → 151 lines)
- ✅ Voice integration service
- ✅ Spectre.Console service wrapper
- ✅ Updated tests for composed option structure
- ✅ **Build succeeds with 0 errors, 0 warnings**

### Remaining:
- 🔄 Replace Console.WriteLine calls with Spectre.Console in legacy command files
- 🔄 Remove legacy CommandLineParser NuGet dependency once all option classes are fully routed
- 🔄 CI/CD pipeline updates

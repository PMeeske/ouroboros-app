# Ouroboros CLI Integration Review

**Date:** 2026-02-26
**Scope:** Full functional review of CLI integration — consistency, usability, tool integration, AGI stack integration
**Solution:** `Ouroboros.App.sln` (.NET 10, System.CommandLine 2.0.3, Spectre.Console 0.54.0)

---

## Executive Summary

The Ouroboros CLI is an ambitious, architecturally-sound system that integrates 15+ subcommands, a deep AGI stack (agents, symbolic reasoning, self-assembly, embodied RL, consciousness), 80+ tools, and multi-provider LLM orchestration into a single binary. The core architecture — **Program.cs → Options → Config → Handler → Service** — is well-designed and consistently applied to the 10 migrated commands. However, the migration is **~75% complete**, and the remaining 25% creates friction: legacy `CommandLineParser` code coexists with the new `System.CommandLine` path, default values diverge across layers, `~1,600 Console.WriteLine` calls bypass the Spectre theme, and the `run_command` agent tool is hardcoded to `powershell.exe` on a project that targets Linux and macOS.

**Overall Score: 7/10** — Strong foundation, needs completion of the migration and consistency passes.

---

## 1. CLI Architecture & Command Structure

### Strengths

| Aspect | Assessment |
|--------|-----------|
| **Framework choice** | System.CommandLine 2.0.3 GA + Spectre.Console 0.54.0 is the best modern .NET CLI stack. Correct decision. |
| **Composition root** | `Host.CreateDefaultBuilder` with `AddCliHost()` mirrors ASP.NET conventions. Single entry point for the entire DI graph. |
| **Composable options** | `IComposableOptions` + `AddToCommand()` eliminates option duplication. `ModelOptions`, `EndpointOptions`, `MultiModelOptions`, etc. are reused across `ask`, `pipeline`, `orchestrator`, `skills`. Clean pattern. |
| **Handler pattern** | `Options → Config → Handler → Service` is consistent across all 10 migrated commands. Handlers are DI-resolved, testable, and separated from parsing logic. |
| **MediatR integration** | Internal command bus (`AskQuery`, `PlanRequest`, `RunMeTTaRequest`, etc.) with 48 handlers decouples the agent's REPL loop from direct service calls. |
| **Upstream provider** | `--api-url` redirects `IAskService`/`IPipelineService` to HTTP-backed implementations. CLI becomes a thin client with zero code changes. |
| **Co-hosted API** | `--serve` embeds the full Web API in the CLI process. Single binary deployment. |
| **Interactive mode** | `interactive` / `i` command provides Spectre.Console selection prompts for progressive discovery. Good onboarding path. |
| **Doctor command** | `doctor` checks .NET SDK, Ollama, Docker, MeTTa, submodules, API keys, platform. Actionable output. |
| **Slash commands** | `SlashCommandRegistry` with fuzzy matching, keyboard shortcuts, and dispatch — extensible REPL extension model. |

### Issues

| Severity | Issue | Location | Impact |
|----------|-------|----------|--------|
| **HIGH** | **Dual CLI parser coexistence.** `CommandLineParser` v2.9.1 is still a dependency. 13 legacy `Options` classes in `/Options/` folder (`PipelineOptions`, `OrchestratorOptions`, `OuroborosOptions`, `DagOptions`, etc.) are still used by 8 un-migrated command classes. Tests in `OptionParsingTests.cs` parse against legacy `AskOptions`/`PipelineOptions` from `CommandLineParser`, not the new `System.CommandLine` options. | `src/Ouroboros.CLI/Options/`, `tests/Ouroboros.CLI.Tests/Parsing/` | Two parsing stacks in one binary; test suite validates the legacy path, not the actual execution path. |
| **HIGH** | **Default model divergence.** `ModelOptions.ModelOption` defaults to `"ministral-3:latest"` but `AskRequest.ModelName` defaults to `"llama3"`. `ImmersiveCommandOptions` and `RoomCommandOptions` default to `"llama3:latest"`. `OuroborosConfig.Model` defaults to `"llama3:latest"`. Four different default model names across the codebase. | `Commands/Options/ModelOptions.cs:14`, `Services/AskRequest.cs:13`, `Commands/Immersive/ImmersiveCommandOptions.cs:23`, `Commands/Ouroboros/OuroborosConfig.cs:8` | User gets different models depending on which command they invoke, with no visible explanation. |
| **MEDIUM** | **Default MaxTokens divergence.** `ModelOptions` → 2048, legacy `PipelineOptions`/`OrchestratorOptions` → 512, `MeTTaConfig` → 512, `OuroborosConfig` → 2048. | Multiple locations | Inconsistent output length behavior across commands. |
| **MEDIUM** | **Pre-parse hack for --api-url/--serve.** Lines 18-28 of `Program.cs` manually scan `args[]` before the DI host is built because these values affect service registration. This circumvents System.CommandLine's parsing and won't handle aliases, `=` syntax, or quoted values correctly. | `Program.cs:18-28` | Edge cases in `--api-url=http://...` or `--api-url "http://..."` may silently fail. |
| **LOW** | **`--voice` option defined twice for `ouroboros`.** Global `--voice` (recursive, default `false`) + `OuroborosCommandOptions.VoiceOption` (default `true`). The ouroboros-specific one shadows the global one. | `Program.cs:127`, `OuroborosCommandOptions.cs:17` | Running `ouroboros` without `--voice` still gets voice=true from the local option. Confusing. |

---

## 2. Tool Integration

### Strengths

| Aspect | Assessment |
|--------|-----------|
| **ToolRegistry pattern** | Immutable, fluent builder pattern. `ToolRegistry.CreateDefault().WithAutonomousTools().WithTool(x)`. Clean functional composition. |
| **ITool interface** | `Name`, `Description`, `JsonSchema`, `InvokeAsync()` returning `Result<string, string>`. Monadic error handling at the tool boundary. |
| **Tool breadth** | 80+ tools across 12 categories: Autonomous, System Access, Git Reflection, Roslyn Analysis, Perception (camera/screen), OpenClaw messaging, Qdrant Admin, Pipeline Step, Web Search, Browser (Playwright), Dynamic (LLM-generated), Service Discovery. |
| **Dynamic tool generation** | `DynamicToolFactory` uses Roslyn to compile LLM-generated C# at runtime. Tools can be born during a session. |
| **Intelligent learner** | `IntelligentToolLearner` with genetic algorithm optimization (`ToolConfigurationChromosome`, `ActionSequenceFitness`) and MeTTa symbolic reasoning for tool selection. |
| **Permission broker** | `ToolPermissionBroker` with Allow/Allow-for-session/Deny model, session approval cache, and `SkipAll` (yolo) mode. Proper safety UX. |
| **Tool renderer** | `ToolRenderer` with status icons (`●`, `✓`, `✗`, `⊘`), truncated parameters, indented body output. Consistent visual language. |

### Issues

| Severity | Issue | Location | Impact |
|----------|-------|----------|--------|
| **CRITICAL** | **`run_command` tool hardcoded to `powershell.exe`.** `AgentToolFactory.RunCommandAsync()` uses `FileName = "powershell.exe"` unconditionally. The project targets `net10.0` (cross-platform), runs CI on Linux, and docker-compose uses Linux containers. | `src/Ouroboros.Application/Agent/AgentToolFactory.cs:182` | Agent mode's command execution is broken on Linux/macOS — will throw `Win32Exception: No such file or directory`. |
| **HIGH** | **SystemAccessTools also hardcodes PowerShell.** `RunPowershell` tool similarly targets Windows-only shell. | `src/Ouroboros.Application/Tools/SystemAccessTools.cs` | Same cross-platform failure. |
| **HIGH** | **Agent tools (9) vs ToolRegistry tools (80+) — disconnected.** `AgentToolFactory.Build()` creates a fixed set of 9 tools (`read_file`, `write_file`, `edit_file`, `list_dir`, `search_files`, `run_command`, `vector_search`, `think`, `ask_user`). These are `Dictionary<string, Func<...>>` lambdas, completely separate from the `ToolRegistry`/`ITool`-based system used by `ToolSubsystem`. The agent cannot use any of the 80+ registered tools (OpenClaw, Perception, Qdrant, Roslyn, etc.). | `src/Ouroboros.Application/Agent/AgentToolFactory.cs` vs `ToolSubsystem` | Two parallel, incompatible tool systems. The agent loop is limited to 9 primitive tools while the rest of the system has 80+. |
| **MEDIUM** | **`AgentPromptBuilder` tool descriptions are hardcoded strings.** The prompt lists 9 tools with hand-written JSON schemas. If `AgentToolFactory.Build()` is extended, the prompt must be manually updated in sync. | `src/Ouroboros.Application/Agent/AgentPromptBuilder.cs:22-68` | Fragile coupling. Adding a tool to the factory without updating the prompt means the LLM doesn't know about it. |
| **MEDIUM** | **No tool timeout or cancellation in `AgentToolFactory`.** `ReadFileAsync`, `SearchFilesAsync`, `RunCommandAsync` have no `CancellationToken` threading. A slow command or large file search blocks indefinitely. | `AgentToolFactory.cs` | Agent loop hangs if a tool call takes too long. |
| **LOW** | **`SearchFilesAsync` reads entire files into memory.** Opens up to 100 files, reads full content, then string-searches. On large repos this is O(files * file_size). | `AgentToolFactory.cs:141-166` | Slow performance on large codebases. Should use `rg`/`grep` subprocess or stream lines. |

---

## 3. AGI Stack Integration

### Strengths

| Aspect | Assessment |
|--------|-----------|
| **Monadic pipeline** | `Step<CliPipelineState, CliPipelineState>` = `Func<CliPipelineState, Task<CliPipelineState>>`. Kleisli composition with DSL tokenization. Clean functional architecture. |
| **Self-improvement loop** | Plan → Execute → Verify → Learn (PEVL) cycle in `OuroborosCliSteps`. Atom-based self-model, confidence assessment, safety constraints. True recursive self-improvement architecture. |
| **MeTTa symbolic reasoning** | Full integration: atom creation, fact assertion, query, unification, explanation. Used in self-assembly blueprint validation and tool learner reasoning. |
| **Self-assembly engine** | Propose → Validate → Approve → Compile → Test → Deploy pipeline for new "neurons". MeTTa safety constraints, Roslyn security analysis, human-in-the-loop approval. |
| **Multi-provider LLM** | `MultiModelRouter` with role-based routing (general, coder, reasoner, summarizer). Graceful fallback from remote to local Ollama. |
| **Consciousness framework** | Pavlovian conditioning, Global Workspace Theory broadcast, cognitive processing, emotion-based arousal. Advanced theoretical grounding. |
| **AutonomousMind** | Background thinking with configurable intervals, curiosity-driven topic rotation, anti-hallucination verification tracking. |
| **Embodied agents** | Unity ML-Agents integration, RL agent with Q-learning, reward shaping, experience replay buffer. |
| **Memory architecture** | 25+ Qdrant collections: thoughts, relations, results, memories, conversations, intentions, self-index, DAG nodes/edges, tool patterns, skills. Comprehensive episodic/semantic/procedural memory. |

### Issues

| Severity | Issue | Location | Impact |
|----------|-------|----------|--------|
| **CRITICAL** | **Theory of Mind: 5 unimplemented methods.** `PredictBehaviorAsync`, `ModelGoalsAsync`, `InferIntentionsAsync`, `PredictActionAsync` all throw `NotImplementedException`. | Engine: `TheoryOfMind.cs` | Multi-agent reasoning is non-functional. |
| **CRITICAL** | **Human Approval Workflow missing.** Plans reaching `EthicalClearanceLevel.RequiresHumanApproval` return `Failure` immediately. | Engine: `MetaAIPlannerOrchestrator.cs:123` | Ethics-gated decisions are permanently blocked. |
| **HIGH** | **World Model limited to MLP.** Transformer, GNN, Hybrid architectures throw `NotImplementedException`. | Engine: `WorldModelEngine.cs:52` | Complex environment modeling severely limited. |
| **HIGH** | **AskQueryHandler creates `new OllamaProvider()` directly.** No DI, no configuration injection. Endpoint resolution uses static `ChatConfig.ResolveWithOverrides()`. Hard to test, hard to configure. | `Mediator/Handlers/AskQueryHandler.cs:67-68,144` | Tight coupling to Ollama. Cannot unit test without a live Ollama instance. |
| **HIGH** | **RAG seeding with hardcoded documents.** Both agent mode and pipeline mode seed the vector store with 3 hardcoded strings ("Event sourcing...", "Circuit breakers...", "CQRS..."). | `AskQueryHandler.cs:90-95,159-163` | RAG always returns the same 3 documents regardless of the user's actual data. Not useful in practice. |
| **MEDIUM** | **`Environment.SetEnvironmentVariable` for runtime config.** `AskQueryHandler` sets `MONADIC_ROUTER` and `MONADIC_DEBUG` as environment variables. Process-global mutation, not thread-safe. | `AskQueryHandler.cs:38-40` | Concurrent requests in `--serve` mode will race on env var values. |
| **MEDIUM** | **Error handling inconsistency across layers.** Foundation uses pure `Result<T,E>` everywhere. Engine mixes Result + exceptions. App mostly uses exceptions with Result wrappers. | Cross-cutting | Inconsistent error propagation; callers can't rely on a single error model. |

---

## 4. Consistency Assessment

### What's Consistent (Good)

1. **Handler → Service → MediatR chain** for all 10 migrated commands
2. **Composable option groups** (`IComposableOptions`) eliminate duplication
3. **OuroborosTheme** purple/gold palette with semantic helpers (`Ok()`, `Err()`, `Warn()`, `Dim()`)
4. **Persona default "Iaret"** aligned across all configs (after migration step)
5. **LocalTts=false** aligned across all configs (prefer cloud TTS)
6. **`ISpectreConsoleService` abstraction** for testable console output
7. **Naming conventions** follow .editorconfig: PascalCase publics, _camelCase privates
8. **xUnit + FluentAssertions + Moq** test stack used consistently

### What's Inconsistent (Problems)

| Category | Inconsistency | Scope |
|----------|---------------|-------|
| **Default model** | `ministral-3:latest` (ModelOptions) vs `llama3` (AskRequest) vs `llama3:latest` (OuroborosConfig, ImmersiveConfig, RoomConfig) | 5 different defaults across 5 locations |
| **Default MaxTokens** | 2048 (ModelOptions, OuroborosConfig, AskRequest) vs 512 (legacy PipelineOptions, OrchestratorOptions, MeTTaConfig) | Split across new/legacy code |
| **Console output** | ~1,600 `Console.WriteLine` calls bypass `ISpectreConsoleService`. OuroborosAgent partial classes (~75), subsystems (~180), legacy commands (~500+), MediatR handlers (~45), Setup files (~150) | 52+ files |
| **Copyright headers** | `"PlaceholderCompany"` copyright in `AgentToolFactory.cs`, `AgentToolExecutor.cs`, `DynamicToolFactory.cs`, `CliTestHarness.cs`, `OptionParsingTests.cs`, and ~50 more files | Engine + App layer |
| **Error patterns** | `Result<T,E>` (foundation, IAgentFacade), exceptions + catch-all (handlers), `$"Error: {ex.Message}"` strings (agent tools) | Three distinct patterns |
| **Service lifetimes** | CLI services registered as `Scoped`, but System.CommandLine creates no scope per invocation. `host.Services.GetRequiredService<T>()` resolves from root provider — scoped services behave as singletons. | `ServiceCollectionExtensions.cs:54-63` |
| **Parsing stack** | Tests validate `CommandLineParser` (`AskOptions`, `PipelineOptions`), but runtime uses `System.CommandLine` (`AskCommandOptions`, `PipelineCommandOptions`). Test coverage does not match production code paths. | `tests/Ouroboros.CLI.Tests/Parsing/` |
| **Tool systems** | `AgentToolFactory` (9 lambda tools, `Dictionary<string, Func<...>>`) is completely disconnected from `ToolRegistry`/`ITool` (80+ tools). Two parallel, incompatible ecosystems. | `Application/Agent/` vs `Application/Tools/` + `CLI/Subsystems/` |

---

## 5. Usability Assessment

### Strengths

1. **Progressive disclosure.** `interactive` mode lets users discover features without memorizing flags. `doctor` immediately diagnoses the environment.
2. **Rich terminal UX.** Spectre.Console provides tables, panels, figlet banners, selection prompts, status spinners, bar charts, tree views. The `quality` dashboard is a showcase.
3. **Voice integration.** Global `--voice` flag enables voice on any command. Azure TTS + STT with Whisper fallback. Wake word support.
4. **Good error messages.** `AskCommandHandler` catches `HttpRequestException`, `TaskCanceledException` separately with actionable suggestions ("Run `dotnet run -- doctor`").
5. **`--serve` and `--api-url` duality.** Can be used as local tool, remote client, or both simultaneously. Elegant deployment topology.

### Usability Problems

| Severity | Problem | Impact |
|----------|---------|--------|
| **HIGH** | **`ouroboros` command has 60+ options.** `OuroborosCommandOptions` defines voice (14), model (8), embedding (3), features (6), autonomous (4), governance (3), task (3), multi-model (4), piping (6), debug (6), cost (3), collective (5), election (5), avatar (5), room (1), OpenClaw (3) options. `--help` output is overwhelming. | New users cannot parse the help text. No progressive disclosure within the `ouroboros` command itself. |
| **HIGH** | **No `--help` examples or grouped help.** System.CommandLine shows flat option lists. With 60+ options, users need examples like `ouroboros --voice --model llama3 "What is AI?"` or grouped sections. | Discoverability is low despite rich functionality. |
| **MEDIUM** | **No shell completions.** System.CommandLine supports `dotnet-suggest` for tab completion but it's not wired up. | Users must type full option names. |
| **MEDIUM** | **`ask` requires `--question` flag.** Unlike most LLM CLIs (`ollama run llama3 "question"`), you must type `ask --question "What is AI?"` instead of `ask "What is AI?"`. No positional argument. | Extra typing for the most common operation. |
| **LOW** | **Command alias inconsistency.** `interactive` has alias `i`, but `ask`, `pipeline`, `ouroboros` have no short aliases. | Experienced users want `o` for ouroboros, `a` for ask, `p` for pipeline. |

---

## 6. Test Coverage Assessment

### Positive

- CLI tests, Application tests, WebApi tests, Integration tests, BDD (ReqnRoll) tests, Android tests all exist
- 60% CI coverage threshold enforced
- `CliTestHarness` provides captured I/O testing with `MockConsole`
- `FluentAssertions` used consistently

### Critical Gap

**The parsing tests validate the wrong system.** All 30+ tests in `OptionParsingTests.cs` use `Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args)` — the `CommandLineParser` legacy path. The production code uses `System.CommandLine` with `AskCommandOptions`, `PipelineCommandOptions`, etc. **There are no tests for the actual System.CommandLine parsing path.** This means the test suite provides false confidence — it validates code that is no longer on the main execution path.

---

## 7. Top Recommendations (Priority Order)

### P0 — Must Fix

1. **Fix `run_command` cross-platform.** Replace `powershell.exe` with cross-platform shell detection (`/bin/sh` on Linux/macOS, `cmd.exe`/`powershell.exe` on Windows).
   - File: `AgentToolFactory.cs:180-183` and `SystemAccessTools.cs`

2. **Unify default model name.** Pick one default (e.g., `llama3:latest`) and use it everywhere: `ModelOptions`, `AskRequest`, `OuroborosConfig`, `ImmersiveCommandOptions`, `RoomCommandOptions`.

3. **Write System.CommandLine parsing tests.** The current `OptionParsingTests` test the legacy `CommandLineParser` path. Add tests that invoke `rootCommand.Parse(args)` against the actual System.CommandLine tree.

### P1 — Should Fix Soon

4. **Bridge AgentToolFactory to ToolRegistry.** The agent's 9 hardcoded tools should be populated from the same `ToolRegistry` that holds 80+ tools. Generate `AgentPromptBuilder` descriptions from `ITool.Description`/`ITool.JsonSchema` automatically.

5. **Complete the Console.WriteLine migration.** ~1,600 calls bypass `ISpectreConsoleService`. Start with `OuroborosAgent` partial classes (highest visibility), then MediatR handlers, then subsystems.

6. **Fix service lifetime mismatch.** `Scoped` services resolved from root provider behave as singletons. Either:
   - Create a scope per command invocation, or
   - Register CLI services as `Transient`/`Singleton` appropriately.

7. **Remove CommandLineParser dependency.** Complete migration of 8 remaining legacy command classes, delete 13 legacy Options files, remove the NuGet package.

### P2 — Should Fix

8. **Add a positional argument to `ask`.** Allow `ask "What is AI?"` in addition to `ask --question "What is AI?"`.

9. **Group `ouroboros` options.** Use System.CommandLine's help customization to group the 60+ options into sections (Voice, Model, Features, Autonomous, etc.) or split into subcommands (`ouroboros voice`, `ouroboros collective`, etc.).

10. **Replace hardcoded RAG seed documents.** `AskQueryHandler` seeds the vector store with 3 hardcoded software engineering strings. This should load from the user's actual data directory or be empty by default.

11. **Replace `PlaceholderCompany` copyright headers.** ~50 files across engine and app layers.

12. **Add shell completions.** Wire up `dotnet-suggest` or generate completions for bash/zsh/fish/PowerShell.

---

## 8. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Program.cs                                  │
│  Pre-parse → Host.CreateDefaultBuilder → AddCliHost() → RootCommand │
│  15 subcommands · global --voice --serve --api-url                  │
└──────────────────────────────┬──────────────────────────────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
   System.CommandLine     Spectre.Console         MediatR
   (parsing + dispatch)   (rich terminal UX)    (internal bus)
        │                      │                      │
        ▼                      ▼                      ▼
┌───────────────┐  ┌────────────────────┐  ┌─────────────────────┐
│ Command       │  │ ISpectreConsole    │  │  48 Handlers         │
│ Options +     │──│ Service +          │──│  (AskQuery,          │
│ Handlers (10) │  │ OuroborosTheme     │  │   PlanRequest,       │
│               │  │ ToolRenderer       │  │   RunMeTTa, etc.)    │
└───────┬───────┘  └────────────────────┘  └──────────┬──────────┘
        │                                             │
        ▼                                             ▼
┌───────────────────────────────────────────────────────────────────┐
│                     Service Layer                                  │
│  IAskService · IPipelineService · IOuroborosAgentService           │
│  IImmersiveModeService · IRoomModeService · ISkillsService         │
│  IOrchestratorService · ICognitivePhysicsService · IMeTTaService   │
│                                                                    │
│  HttpApiAskService / HttpApiPipelineService (--api-url mode)       │
└───────────────────────────────┬───────────────────────────────────┘
                                │
┌───────────────────────────────┼───────────────────────────────────┐
│              Ouroboros.Application Layer                           │
│                               │                                    │
│  ┌──────────────┐  ┌──────────────────┐  ┌──────────────────────┐│
│  │ Pipeline DSL  │  │ Agent Framework  │  │ Tool Ecosystem       ││
│  │ Step<S,S>     │  │ AutoAgent (9     │  │ ToolRegistry (80+    ││
│  │ Monadic Kleisli│ │ tools, JSON     │  │ ITool impls)         ││
│  │ CliPipelineState│ │ protocol)       │  │ DynamicToolFactory   ││
│  └──────────────┘  └──────────────────┘  │ IntelligentLearner   ││
│                                           └──────────────────────┘│
│  ┌──────────────┐  ┌──────────────────┐  ┌──────────────────────┐│
│  │ Self-Assembly │  │ Consciousness    │  │ Memory (25+ Qdrant   ││
│  │ PEVL Cycle    │  │ GWT + Pavlovian  │  │ collections)         ││
│  │ MeTTa Safety  │  │ InnerDialog      │  │ Episodic + Semantic  ││
│  └──────────────┘  │ AutonomousMind   │  │ + Procedural         ││
│                     └──────────────────┘  └──────────────────────┘│
└───────────────────────────────┬───────────────────────────────────┘
                                │
┌───────────────────────────────┼───────────────────────────────────┐
│                    Foundation + Engine Layers                      │
│  Core · Domain · Tools · Pipeline · Providers · Agent · Network   │
│  Roslynator · Genetic · MeTTa                                     │
│  LangChain 0.17.0 · Ollama · OpenAI · Anthropic                  │
└───────────────────────────────────────────────────────────────────┘
```

---

## 9. File Index — Key Sources Referenced

| File | Role |
|------|------|
| `src/Ouroboros.CLI/Program.cs` | Composition root, 15 subcommands, global options |
| `src/Ouroboros.CLI/Hosting/ServiceCollectionExtensions.cs` | DI registration: engine + CLI services + handlers |
| `src/Ouroboros.CLI/Commands/Options/*.cs` | Composable option groups (8 groups) |
| `src/Ouroboros.CLI/Commands/Ask/AskCommandHandler.cs` | Model handler with error UX |
| `src/Ouroboros.CLI/Commands/Ouroboros/OuroborosConfig.cs` | 60+ field immutable record |
| `src/Ouroboros.CLI/Mediator/Handlers/AskQueryHandler.cs` | Pipeline/Agent/Router execution |
| `src/Ouroboros.CLI/Infrastructure/ToolPermissionBroker.cs` | Allow/Deny/Session approval UX |
| `src/Ouroboros.CLI/Infrastructure/ToolRenderer.cs` | Consistent tool-call display |
| `src/Ouroboros.CLI/Infrastructure/SlashCommandRegistry.cs` | REPL slash-command system |
| `src/Ouroboros.CLI/Infrastructure/OuroborosTheme.cs` | Purple/gold theme constants |
| `src/Ouroboros.Application/Agent/AgentToolFactory.cs` | 9 agent tools (powershell.exe issue) |
| `src/Ouroboros.Application/Agent/AgentPromptBuilder.cs` | Hardcoded tool descriptions |
| `src/Ouroboros.Application/Tools/DynamicToolFactory.cs` | Runtime tool generation via Roslyn |
| `src/Ouroboros.CLI/MIGRATION_PLAN.md` | Self-documenting migration status (75%) |
| `tests/Ouroboros.CLI.Tests/Parsing/OptionParsingTests.cs` | Tests legacy parser, not production path |

---

## 10. Conclusion

The Ouroboros CLI has a **genuinely excellent architectural blueprint**. The `Options → Config → Handler → Service → MediatR` layering, composable option groups, monadic pipeline DSL, and the breadth of the AGI stack (self-assembly, symbolic reasoning, consciousness, embodied RL) are impressive. The tooling layer (80+ tools, genetic optimization, dynamic generation) is ambitious and mostly well-integrated.

The primary risk is **the incomplete migration**. Two CLI parsers, two tool systems, ~1,600 raw Console calls, and divergent defaults create a "two systems in one binary" feel. Completing the migration — especially unifying the tool systems, fixing cross-platform shell execution, and migrating the tests to validate the production path — would elevate this from a 7/10 to a solid 9/10.

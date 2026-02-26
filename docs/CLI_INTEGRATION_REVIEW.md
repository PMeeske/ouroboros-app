# Ouroboros CLI Integration Review

**Date:** 2026-02-26
**Scope:** Full functional review of CLI integration — consistency, usability, tool integration, AGI stack integration
**Solution:** `Ouroboros.App.sln` (.NET 10, System.CommandLine 2.0.3, Spectre.Console 0.54.0)
**Last Updated:** 2026-02-26 (post-fix pass)

---

## Executive Summary

The Ouroboros CLI is an ambitious, architecturally-sound system that integrates 15+ subcommands, a deep AGI stack (agents, symbolic reasoning, self-assembly, embodied RL, consciousness), 80+ tools, and multi-provider LLM orchestration into a single binary. The core architecture — **Program.cs → Options → Config → Handler → Service** — is well-designed and consistently applied to the 10 migrated commands. The migration is **~75% complete** — legacy `CommandLineParser` code coexists with the new `System.CommandLine` path and `~1,600 Console.WriteLine` calls bypass the Spectre theme — but the most critical functional issues have been resolved: cross-platform shell execution, unified default model (`deepseek-v3.1:671b-cloud`), service lifetime correctness, agent tool timeouts and cancellation, and auto-generated prompt descriptions.

**Overall Score: 8/10** — Strong foundation with critical bugs fixed; remaining work is migration completion and consistency passes.

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

| Severity | Issue | Location | Status |
|----------|-------|----------|--------|
| **HIGH** | **Dual CLI parser coexistence.** `CommandLineParser` v2.9.1 is still a dependency. 13 legacy `Options` classes in `/Options/` folder are still used by 8 un-migrated command classes. Tests in `OptionParsingTests.cs` parse against legacy `AskOptions`/`PipelineOptions`, not the new `System.CommandLine` options. | `src/Ouroboros.CLI/Options/`, `tests/Ouroboros.CLI.Tests/Parsing/` | **OPEN** — requires full migration of remaining legacy commands |
| ~~**HIGH**~~ | ~~**Default model divergence.** Four different default model names across the codebase.~~ | Multiple locations | **FIXED** — unified to `deepseek-v3.1:671b-cloud` across all 21 affected files |
| ~~**MEDIUM**~~ | ~~**Default MaxTokens divergence.** 2048 vs 512 across different options.~~ | Multiple locations | **FIXED** — unified to 2048 across all configs |
| ~~**MEDIUM**~~ | ~~**Pre-parse hack for --api-url/--serve.** Didn't handle `=` syntax or quoted values.~~ | `Program.cs:18-31` | **FIXED** — now handles `--api-url=VALUE` syntax correctly |
| ~~**LOW**~~ | ~~**`--voice` option defined twice for `ouroboros`.** Local option shadowed global with default `true`.~~ | `OuroborosCommandOptions.cs` | **FIXED** — default changed to `false`, consistent with global `--voice` |

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

| Severity | Issue | Location | Status |
|----------|-------|----------|--------|
| ~~**CRITICAL**~~ | ~~**`run_command` tool hardcoded to `powershell.exe`.**~~ | `AgentToolFactory.cs` | **FIXED** — cross-platform shell detection via `GetShellCommand()`: `/bin/sh -c` on Linux/macOS, `cmd.exe /C` on Windows. 30s timeout with `CancellationTokenSource.CreateLinkedTokenSource`. |
| ~~**HIGH**~~ | ~~**SystemAccessTools also hardcodes PowerShell.**~~ | `SystemAccessTools.cs` | **FIXED** — PowerShellTool, ClipboardTool, NetworkInfoTool now use `RuntimeInformation.IsOSPlatform()` for cross-platform commands. |
| **HIGH** | **Agent tools (9) vs ToolRegistry tools (80+) — disconnected.** `AgentToolFactory.Build()` creates a fixed set of 9 tools, completely separate from the `ToolRegistry`/`ITool`-based system used by `ToolSubsystem`. | `AgentToolFactory.cs` vs `ToolSubsystem` | **OPEN** — Two parallel, incompatible tool systems. Bridging requires adapter layer. |
| ~~**MEDIUM**~~ | ~~**`AgentPromptBuilder` tool descriptions are hardcoded strings.**~~ | `AgentPromptBuilder.cs` | **FIXED** — `BuildToolDescriptions()` now auto-generates from `AgentToolFactory.ToolDescriptors` (`AgentToolDescriptor` record). Adding a tool to the factory automatically updates the prompt. |
| ~~**MEDIUM**~~ | ~~**No tool timeout or cancellation in `AgentToolFactory`.**~~ | `AgentToolFactory.cs` | **FIXED** — All tool methods now thread `CliPipelineState.CancellationToken`. `RunCommandAsync` enforces a 30s timeout. Cancellation is cooperative throughout. |
| ~~**LOW**~~ | ~~**`SearchFilesAsync` reads entire files into memory.**~~ | `AgentToolFactory.cs` | **FIXED** — Now uses `File.ReadLinesAsync()` to stream lines instead of loading entire files into memory. |

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

| Severity | Issue | Location | Status |
|----------|-------|----------|--------|
| **CRITICAL** | **Theory of Mind: 5 unimplemented methods.** `PredictBehaviorAsync`, `ModelGoalsAsync`, `InferIntentionsAsync`, `PredictActionAsync` all throw `NotImplementedException`. | Engine: `TheoryOfMind.cs` | **OPEN** — Multi-agent reasoning is non-functional. |
| **CRITICAL** | **Human Approval Workflow missing.** Plans reaching `EthicalClearanceLevel.RequiresHumanApproval` return `Failure` immediately. | Engine: `MetaAIPlannerOrchestrator.cs:123` | **OPEN** — Ethics-gated decisions are permanently blocked. |
| **HIGH** | **World Model limited to MLP.** Transformer, GNN, Hybrid architectures throw `NotImplementedException`. | Engine: `WorldModelEngine.cs:52` | **OPEN** — Complex environment modeling severely limited. |
| **HIGH** | **AskQueryHandler creates `new OllamaProvider()` directly.** No DI, no configuration injection. | `AskQueryHandler.cs` | **OPEN** — Tight coupling to Ollama. Cannot unit test without a live instance. |
| ~~**HIGH**~~ | ~~**RAG seeding with hardcoded documents.** Both agent mode and pipeline mode seeded the vector store with 3 hardcoded strings.~~ | `AskQueryHandler.cs` | **FIXED** — Hardcoded seed documents removed. RAG now operates on user's actual data only. |
| ~~**MEDIUM**~~ | ~~**`Environment.SetEnvironmentVariable` for runtime config.** Process-global mutation, not thread-safe.~~ | `AskQueryHandler.cs` | **FIXED** — Replaced with `ChatRuntimeSettings` properties (`UseRouter`, `Debug`). No process-global side effects. |
| **MEDIUM** | **Error handling inconsistency across layers.** Foundation uses pure `Result<T,E>` everywhere. Engine mixes Result + exceptions. App mostly uses exceptions. | Cross-cutting | **OPEN** — Inconsistent error propagation across the stack. |

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

### What's Inconsistent (Remaining Problems)

| Category | Inconsistency | Scope | Status |
|----------|---------------|-------|--------|
| ~~**Default model**~~ | ~~5 different defaults across 5 locations~~ | — | **FIXED** — unified to `deepseek-v3.1:671b-cloud` |
| ~~**Default MaxTokens**~~ | ~~2048 vs 512 across configs~~ | — | **FIXED** — unified to 2048 |
| **Console output** | ~1,600 `Console.WriteLine` calls bypass `ISpectreConsoleService` | 52+ files | **OPEN** |
| ~~**Copyright headers**~~ | ~~`"PlaceholderCompany"` in ~150 files~~ | — | **FIXED** — all updated to `"Ouroboros"` |
| **Error patterns** | `Result<T,E>` (foundation), exceptions (handlers), `$"Error: {ex.Message}"` strings (agent tools) | Three distinct patterns | **OPEN** |
| ~~**Service lifetimes**~~ | ~~Scoped services resolved from root provider behaved as singletons~~ | — | **FIXED** — all CLI services registered as `Transient` |
| **Parsing stack** | Tests validate `CommandLineParser`, but runtime uses `System.CommandLine` | `tests/Ouroboros.CLI.Tests/Parsing/` | **OPEN** |
| **Tool systems** | `AgentToolFactory` (9 tools) disconnected from `ToolRegistry` (80+ tools) | `Application/Agent/` vs `Application/Tools/` | **OPEN** |

---

## 5. Usability Assessment

### Strengths

1. **Progressive disclosure.** `interactive` mode lets users discover features without memorizing flags. `doctor` immediately diagnoses the environment.
2. **Rich terminal UX.** Spectre.Console provides tables, panels, figlet banners, selection prompts, status spinners, bar charts, tree views. The `quality` dashboard is a showcase.
3. **Voice integration.** Global `--voice` flag enables voice on any command. Azure TTS + STT with Whisper fallback. Wake word support.
4. **Good error messages.** `AskCommandHandler` catches `HttpRequestException`, `TaskCanceledException` separately with actionable suggestions ("Run `dotnet run -- doctor`").
5. **`--serve` and `--api-url` duality.** Can be used as local tool, remote client, or both simultaneously. Elegant deployment topology.

### Usability Problems

| Severity | Problem | Impact | Status |
|----------|---------|--------|--------|
| **HIGH** | **`ouroboros` command has 60+ options.** `--help` output is overwhelming. | New users cannot parse the help text. | **OPEN** |
| **HIGH** | **No `--help` examples or grouped help.** System.CommandLine shows flat option lists. | Discoverability is low despite rich functionality. | **OPEN** |
| **MEDIUM** | **No shell completions.** `dotnet-suggest` not wired up. | Users must type full option names. | **OPEN** |
| ~~**MEDIUM**~~ | ~~**`ask` requires `--question` flag.** No positional argument.~~ | — | **FIXED** — `ask "What is AI?"` now works via positional `Argument<string?>`. `--question` still supported as fallback. |
| ~~**LOW**~~ | ~~**Command alias inconsistency.** No short aliases for main commands.~~ | — | **FIXED** — Added `a`=ask, `p`=pipeline, `o`=ouroboros aliases. |

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

1. ~~**Fix `run_command` cross-platform.**~~ **DONE** — Cross-platform shell detection via `GetShellCommand()` in `AgentToolFactory.cs` and `SystemAccessTools.cs`.

2. ~~**Unify default model name.**~~ **DONE** — Unified to `deepseek-v3.1:671b-cloud` across all 21 affected files.

3. **Write System.CommandLine parsing tests.** The current `OptionParsingTests` test the legacy `CommandLineParser` path. Add tests that invoke `rootCommand.Parse(args)` against the actual System.CommandLine tree. **STILL NEEDED.**

### P1 — Should Fix Soon

4. **Bridge AgentToolFactory to ToolRegistry.** The agent's 9 hardcoded tools should be populated from the same `ToolRegistry` that holds 80+ tools. `AgentPromptBuilder` now auto-generates from `ToolDescriptors`, which is a step in the right direction, but the two tool systems remain disconnected. **PARTIALLY DONE.**

5. **Complete the Console.WriteLine migration.** ~1,600 calls bypass `ISpectreConsoleService`. Start with `OuroborosAgent` partial classes (highest visibility), then MediatR handlers, then subsystems. **STILL NEEDED.**

6. ~~**Fix service lifetime mismatch.**~~ **DONE** — All CLI services and handlers registered as `Transient`. `AddUpstreamApiProvider` also corrected from `Scoped` to `Transient`.

7. **Remove CommandLineParser dependency.** Complete migration of 8 remaining legacy command classes, delete 13 legacy Options files, remove the NuGet package. **STILL NEEDED.**

### P2 — Should Fix

8. ~~**Add a positional argument to `ask`.**~~ **DONE** — `ask "What is AI?"` now works; `--question` still supported as fallback.

9. **Group `ouroboros` options.** Use System.CommandLine's help customization to group the 60+ options into sections or split into subcommands. **STILL NEEDED.**

10. ~~**Replace hardcoded RAG seed documents.**~~ **DONE** — Hardcoded documents removed from `AskQueryHandler`.

11. ~~**Replace `PlaceholderCompany` copyright headers.**~~ **DONE** — Updated to `"Ouroboros"` across ~150 files.

12. **Add shell completions.** Wire up `dotnet-suggest` or generate completions for bash/zsh/fish/PowerShell. **STILL NEEDED.**

### Additional Fixes Applied (not in original recommendations)

13. ~~**Agent tool timeout/cancellation.**~~ **DONE** — `CliPipelineState.CancellationToken` added and threaded through all `AgentToolFactory` tools. `RunCommandAsync` enforces a 30s timeout.

14. ~~**`AgentPromptBuilder` auto-generation.**~~ **DONE** — `BuildToolDescriptions()` generates from `AgentToolFactory.ToolDescriptors`. No more manual sync required.

15. ~~**`Environment.SetEnvironmentVariable` removal.**~~ **DONE** — Router/debug config moved into `ChatRuntimeSettings` properties.

16. ~~**`--voice` shadowing.**~~ **DONE** — `OuroborosCommandOptions.VoiceOption` default changed from `true` to `false`.

17. ~~**Command aliases.**~~ **DONE** — `a`=ask, `p`=pipeline, `o`=ouroboros added.

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
| `src/Ouroboros.Application/Agent/AgentToolFactory.cs` | 9 agent tools (cross-platform, with timeout/cancellation) |
| `src/Ouroboros.Application/Agent/AgentPromptBuilder.cs` | Auto-generated tool descriptions from ToolDescriptors |
| `src/Ouroboros.Application/Tools/DynamicToolFactory.cs` | Runtime tool generation via Roslyn |
| `src/Ouroboros.CLI/MIGRATION_PLAN.md` | Self-documenting migration status (75%) |
| `tests/Ouroboros.CLI.Tests/Parsing/OptionParsingTests.cs` | Tests legacy parser, not production path |

---

## 10. Conclusion

The Ouroboros CLI has a **genuinely excellent architectural blueprint**. The `Options → Config → Handler → Service → MediatR` layering, composable option groups, monadic pipeline DSL, and the breadth of the AGI stack (self-assembly, symbolic reasoning, consciousness, embodied RL) are impressive. The tooling layer (80+ tools, genetic optimization, dynamic generation) is ambitious and mostly well-integrated.

**After the fix pass, the most critical functional bugs are resolved:** cross-platform shell execution works, defaults are unified, service lifetimes are correct, agent tools have timeouts and cancellation, and prompt generation is automated. The score has improved from 7/10 to **8/10**.

The remaining work is primarily **migration completion**: removing the legacy `CommandLineParser` dependency, writing System.CommandLine parsing tests, migrating ~1,600 `Console.WriteLine` calls to `ISpectreConsoleService`, bridging the two tool systems, and adding grouped help / shell completions. Completing these items would elevate the CLI to a solid 9/10.

### Summary of Fixes Applied

| Fix | Files Changed |
|-----|---------------|
| Cross-platform shell execution (`AgentToolFactory` + `SystemAccessTools`) | 2 |
| Unified default model → `deepseek-v3.1:671b-cloud` | 21 |
| Unified MaxTokens → 2048 | 3 |
| Service lifetimes Scoped → Transient | 1 |
| `AddUpstreamApiProvider` Scoped → Transient | 1 |
| Copyright `PlaceholderCompany` → `Ouroboros` | ~150 |
| Agent tool CancellationToken + 30s timeout | 2 (AgentToolFactory + CliPipelineState) |
| `AgentPromptBuilder` auto-generation from `ToolDescriptors` | 1 |
| `SearchFilesAsync` line streaming | 1 |
| `Environment.SetEnvironmentVariable` → `ChatRuntimeSettings` | 1 |
| Pre-parse hack `--api-url=VALUE` support | 1 |
| `--voice` shadowing fix | 1 |
| Positional argument for `ask` | 2 |
| Command aliases (`a`, `p`, `o`) | 1 |
| Hardcoded RAG seed documents removed | 1 |

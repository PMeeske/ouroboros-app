# Implementation Plan: CLI Architecture Cleanup & Consolidation

## Goal
Complete the refactoring migration so every command follows:
**Program.cs → Options → Config → Handler → Service Interface → Service Implementation**

Eliminate all dead code, duplicates, and inconsistencies. An outsider reading the codebase should immediately understand the pattern.

---

## Phase 1 — Delete Dead Code

### 1a. Delete 11 unreachable old-style command classes
None of these are routed in Program.cs. They contain static mutable state, raw `Console.WriteLine`, and no DI.

Files to delete:
- `Commands/Affect/AffectCommands.cs`
- `Commands/Benchmark/BenchmarkCommands.cs`
- `Commands/Dag/DagCommands.cs`
- `Commands/Distinction/DistinctionCommands.cs`
- `Commands/Dream/DreamCommands.cs`
- `Commands/Environment/EnvironmentCommands.cs`
- `Commands/Maintenance/MaintenanceCommands.cs`
- `Commands/Network/NetworkCommands.cs`
- `Commands/Policy/PolicyCommands.cs`
- `Commands/Self/SelfCommands.cs`
- `Commands/Test/TestCommands.cs`

**Verification before each delete:** `grep -r "ClassName" src/Ouroboros.CLI` confirms no references from routed code.

### 1b. Delete 2 orphaned companion command files
These are superseded by handlers already wired in Program.cs (`PipelineCommandHandler`, `OrchestratorCommandHandler`):
- `Commands/Pipeline/PipelineCommands.cs`
- `Commands/Orchestrator/OrchestratorCommands.cs`

### 1c. Delete unused DI registration helpers
These per-handler `Add*` methods are never called — all handlers are registered centrally in `ServiceCollectionExtensions.AddCommandHandlers()`:
- `AskCommandHandlerExtensions.AddAskCommandHandler()` — remove the method from `Commands/Ask/AskCommandHandlerExtensions.cs`
- `MeTTaCommandHandlerExtensions.AddMeTTaCommandHandler()` — remove the method from `Commands/MeTTa/MeTTaCommandHandlerExtensions.cs`

### 1d. Build verification
Run `dotnet build` to confirm no compilation errors after deletions.

---

## Phase 2 — Fix MeTTa (The Last Outlier)

MeTTa is the only command that still delegates to a static class (`MeTTaCommands`) and uses the legacy `Ouroboros.Options.MeTTaOptions` type instead of a proper `MeTTaConfig` record.

### 2a. Create `MeTTaConfig` record
- File: `Commands/MeTTa/MeTTaConfig.cs`
- Namespace: `Ouroboros.CLI.Commands`
- Sealed record with all properties currently on `MeTTaOptions` (Goal, Culture, Model, Temperature, MaxTokens, TimeoutSeconds, Endpoint, ApiKey, EndpointType, Debug, Embed, EmbedModel, QdrantEndpoint, PlanOnly, ShowMetrics, Interactive, Persona, Voice, VoiceOnly, LocalTts, VoiceLoop)
- Immutable with sensible defaults matching current `MeTTaOptions`

### 2b. Create `IMeTTaService` interface + `MeTTaService` implementation
- Interface: `Services/IMeTTaService.cs`
  - `Task<int> RunAsync(MeTTaConfig config, CancellationToken cancellationToken)`
- Implementation: `Services/MeTTaService.cs`
  - Move all logic from static `MeTTaCommands.RunMeTTaAsync()` and `RunMeTTaVoiceModeAsync()` into instance methods
  - Inject `ISpectreConsoleService` (replace `Console.WriteLine` calls)
  - Inject `ILogger<MeTTaService>`
  - Accept `MeTTaConfig` instead of `MeTTaOptions`

### 2c. Refactor `MeTTaCommandHandler`
- Move from `namespace Ouroboros.CLI.Commands` → `namespace Ouroboros.CLI.Commands.Handlers`
- Inject `IMeTTaService` instead of calling static `MeTTaCommands` directly
- Implement `ICommandHandler<MeTTaConfig>`
- Accept `MeTTaConfig` in `HandleAsync` instead of `MeTTaOptions`

### 2d. Update `MeTTaCommandHandlerExtensions`
- Move to `namespace Ouroboros.CLI.Commands.Handlers`
- Update `ConfigureMeTTaCommand` to build `MeTTaConfig` (not `MeTTaOptions`) from `ParseResult`
- Remove the unused `AddMeTTaCommandHandler()` method (from Phase 1c)

### 2e. Register `IMeTTaService`/`MeTTaService` in DI
- Add `services.TryAddScoped<IMeTTaService, MeTTaService>()` to `ServiceCollectionExtensions.AddCliServices()`

### 2f. Delete static `MeTTaCommands.cs`
- Once all logic is in `MeTTaService`, delete `Commands/MeTTa/MeTTaCommands.cs`
- Also delete `Commands/MeTTa/MeTTaInteractiveMode.cs` if it's absorbed into `MeTTaService`, or move it to be an internal helper if still needed

### 2g. Build verification
`dotnet build` — 0 errors, 0 warnings.

---

## Phase 3 — Unify Voice Support

### 3a. Pick one voice service, delete the other
Current state:
- **VoiceModeService (V1):** 945 lines, `IDisposable`, registered in DI via `AddExistingBusinessLogic()`, persona "Iaret" (hardcoded dict)
- **VoiceModeServiceV2 (V2):** 682 lines, `IAsyncDisposable`, NOT registered in DI, persona "Iaret" (configurable via switch), multi-persona support

**Decision: Keep V2, delete V1.** Rationale:
- V2 is shorter/cleaner (682 vs 945 lines)
- V2 supports `IAsyncDisposable` (correct for async I/O resources)
- V2 has multi-persona support
- V2 has configurable timeouts (barge-in debounce, idle timeout)

**Actions:**
1. Create `IVoiceModeService` interface in `Services/IVoiceModeService.cs` matching V2's public surface
2. Make V2 implement `IVoiceModeService`, rename it to `VoiceModeService` (drop the V2 suffix)
3. Register `IVoiceModeService`/`VoiceModeService` in `ServiceCollectionExtensions.AddInfrastructureServices()`
4. Update `VoiceIntegrationService` to use the new `IVoiceModeService` instead of concrete `VoiceModeService`
5. Delete old `VoiceModeService.cs` (V1)
6. Delete `VoiceModeConfig.cs` (V1 config), keep `VoiceModeConfigV2.cs` and rename to `VoiceModeConfig.cs`
7. Rename `VoiceModeServiceV2.cs` → `VoiceModeService.cs`
8. Rename `VoiceModeConfigV2.cs` → `VoiceModeConfig.cs`
9. Remove the old registration from `AddExistingBusinessLogic()` (if method becomes empty, delete it)

### 3b. Align `LocalTts` defaults
Current state:
- `OuroborosConfig.LocalTts = true`
- `ImmersiveConfig.LocalTts = false`
- `RoomConfig.LocalTts = false`
- `VoiceModeConfig (V1).LocalTts = true`
- `VoiceModeConfigV2.EnableTts = true` (different property name)

**Decision: Default `LocalTts = false` everywhere.** Rationale:
- `false` means "prefer cloud/Azure TTS" which is the better-quality default for persona modes
- Immersive and Room already default to `false`
- Users can opt into local TTS explicitly with `--local-tts`

**Actions:**
1. Change `OuroborosConfig.LocalTts` default from `true` → `false`
2. Add a code comment explaining: "Defaults to false (prefer cloud TTS). Use --local-tts for offline/low-latency scenarios."
3. Verify ImmersiveConfig and RoomConfig already have `false` (they do)

### 3c. Document Room's voice intent
Room mode is always listening (ambient presence). This should be self-documenting.

**Action:** Add `/// <summary>` doc comment to `RoomConfig` explaining that Room is always-on voice by design and doesn't need a `--voice` flag. Alternatively, add an explicit `Voice = true` property that's always true by default with a doc comment.

**Decision: Add a code comment** explaining why `Voice` property is absent — Room is always-listening by design. Adding a property that's always true is misleading (suggests it can be turned off).

### 3d. Clean up CognitivePhysics voice dead code
`CognitivePhysicsCommandHandlerExtensions.ConfigureCognitivePhysicsCommand()` receives `globalVoiceOption` but never uses it.

**Action:** Remove the `Option<bool> globalVoiceOption` parameter from `ConfigureCognitivePhysicsCommand()`. Update the call site in `Program.cs` (`CreateCognitivePhysicsCommand`) to not pass `voiceOption`. Since cognitive-physics is a math/physics engine command (not conversational), voice doesn't apply.

### 3e. Build verification
`dotnet build` — 0 errors, 0 warnings.

---

## Phase 4 — Spectre.Console Migration

### 4a. Refactor DoctorCommand, ChatCommand, InteractiveCommand
These are currently static classes accepting raw `IAnsiConsole`. They should use DI.

**DoctorCommand:**
- Convert from static to instance class
- Inject `ISpectreConsoleService` via constructor
- Register in DI via `AddCommandHandlers()`
- Update `CreateDoctorCommand()` in Program.cs to resolve from DI instead of passing `AnsiConsole.Console`

**ChatCommand:**
- Convert from static to instance class
- Inject `ISpectreConsoleService`, `IAskService` via constructor
- Register in DI
- Note: ChatCommand uses raw `Console.ReadKey()` and `Console.Write()` for REPL input — these can remain as they handle terminal I/O directly (not display output). Mark with comments explaining why.
- Replace display-oriented `Console.WriteLine` calls with `ISpectreConsoleService`

**InteractiveCommand:**
- Convert from static to instance class
- Inject `ISpectreConsoleService`, `IAskService`, `IPipelineService` via constructor
- Register in DI

### 4b. Replace Console.WriteLine in OuroborosAgent partial classes
Files: `OuroborosAgent.cs`, `OuroborosAgent.RunLoop.cs`, `OuroborosAgent.Init.cs`, `OuroborosAgent.Voice.cs`, plus `.Cognition.cs`, `.Commands.cs`, `.Persistence.cs`, `.Steps.cs`, `.Tools.cs`

- `OuroborosAgent` is constructed with dependencies — add `ISpectreConsoleService` as a constructor parameter
- Replace all `Console.WriteLine` → `_console.WriteLine()` or `_console.MarkupLine()`
- These files contain ~40+ `Console.WriteLine` calls total

### 4c. Replace Console.WriteLine in Subsystem files
11 subsystem implementation files (AutonomySubsystem, CognitiveSubsystem, EmbodimentSubsystem, ImmersiveSubsystem, MemorySubsystem, ModelSubsystem, PipeProcessingSubsystem, SelfAssemblySubsystem, ToolSubsystem, VoiceSubsystem, ChatSubsystem)

- Subsystems have `Initialize(SubsystemInitContext context)` pattern
- Add `ISpectreConsoleService` to `SubsystemInitContext` or inject via each subsystem's constructor
- Replace `Console.WriteLine` calls

### 4d. Replace Console.WriteLine in OuroborosCliIntegration.cs
- 13 `Console.WriteLine` calls in `Setup/OuroborosCliIntegration.cs`
- Pass `ISpectreConsoleService` as parameter or inject

### 4e. Clean up Program.cs direct AnsiConsole calls
Current direct `AnsiConsole.MarkupLine()` usages in Program.cs:
- Line 68: `AnsiConsole.MarkupLine("...");` in serve startup
- Line 107-109: Version display
- Line 229: DoctorCommand
- Line 309: Serve command

For DoctorCommand (line 229): Resolve `ISpectreConsoleService` from host instead of passing `AnsiConsole.Console`.
For ChatCommand (line 242): Same — resolve from DI.
For InteractiveCommand (line 257): Same.

The `CreateServeCommand()` and root command version display are not inside DI scope — these can remain as `AnsiConsole.MarkupLine()` since they run before/outside the DI container.

### 4f. Fix QualityCommandHandler escape hatch
`QualityCommandHandler` injects `ISpectreConsoleService` then unwraps to raw `IAnsiConsole` via `_console.Console`:
```csharp
var c = _console.Console;
RenderHeader(c);  // all helpers accept IAnsiConsole
```

**Action:** Refactor helper methods to accept `ISpectreConsoleService` directly, or keep `IAnsiConsole` in helpers since they use Spectre's rich rendering (Table, Panel, BarChart, etc.) which requires `IAnsiConsole`. The issue is minor — the handler correctly uses DI for the top-level injection. The helpers can continue to use `IAnsiConsole` since they're private and the console reference comes from the injected service. Add a comment explaining this is intentional.

**Revised decision:** Since Spectre.Console widgets like `Table`, `Panel`, `BarChart` require `IAnsiConsole.Write(IRenderable)` which `ISpectreConsoleService` doesn't expose, the helpers legitimately need `IAnsiConsole`. The fix is to either:
1. Expose an `IAnsiConsole Console { get; }` property on `ISpectreConsoleService` (if not already present) and document why the escape hatch exists
2. Or add `Write(IRenderable)` to `ISpectreConsoleService`

**Action:** Add `void Write(IRenderable renderable)` to `ISpectreConsoleService` and implement it in `SpectreConsoleService`. Then refactor `QualityCommandHandler` helpers to use `ISpectreConsoleService` directly.

### 4g. Build verification
`dotnet build` — 0 errors, 0 warnings.

---

## Phase 5 — Delete Legacy Options Layer

### 5a. Verify zero remaining references
After Phase 2 (MeTTa refactored), the legacy `Options/` folder should have zero references from routed code. Files to verify and delete:

- `Options/MeTTaOptions.cs` — replaced by `MeTTaConfig`
- `Options/OrchestratorOptions.cs` — replaced by orchestrator handler
- `Options/VoiceOptionsBase.cs` — replaced by `CommandVoiceOptions`
- `Options/IVoiceOptions.cs` — replaced by voice config records
- `Options/TestOptions.cs` — dead (TestCommands deleted in Phase 1)
- `Options/BenchmarkOptions.cs` — dead
- `Options/AffectOptions.cs` — dead
- `Options/AskOptions.cs` — superseded by `AskCommandOptions`
- `Options/AssistOptions.cs` — dead
- `Options/BaseModelOptions.cs` — dead
- `Options/DagOptions.cs` — dead
- `Options/DistinctionClearOptions.cs` — dead
- `Options/DistinctionDissolveOptions.cs` — dead
- `Options/DistinctionExportOptions.cs` — dead
- `Options/DistinctionLearnOptions.cs` — dead
- `Options/DistinctionListOptions.cs` — dead
- `Options/DistinctionOptions.cs` — dead
- `Options/DistinctionStatusOptions.cs` — dead
- `Options/DreamOptions.cs` — dead
- `Options/EnvironmentOptions.cs` — dead
- `Options/ExplainOptions.cs` — dead
- `Options/ImmersiveCommandVoiceOptions.cs` — replaced by `ImmersiveCommandOptions`
- `Options/ListTokensOptions.cs` — dead
- `Options/MaintenanceOptions.cs` — dead
- `Options/NetworkOptions.cs` — dead
- `Options/OuroborosOptions.cs` — replaced by `OuroborosCommandOptions`
- `Options/PipelineOptions.cs` — replaced by `PipelineCommandOptions`
- `Options/PolicyOptions.cs` — dead
- `Options/SelfOptions.cs` — dead
- `Options/SetupOptions.cs` — dead
- `Options/SkillsOptions.cs` — replaced by `SkillsCommandOptions`

For each file: `grep -r "ClassName" src/Ouroboros.CLI` to verify zero references before deleting.

**Critical:** Some non-dead-code files still reference `Ouroboros.Options`:
- `Services/ImmersiveModeService.cs` — uses `Ouroboros.Options` (must be updated in Phase 2/4)
- `Services/OrchestratorService.cs` — uses `Ouroboros.Options`
- `Services/PipelineService.cs` — uses `Ouroboros.Options`
- `Mediator/Requests/RunMeTTaRequest.cs` — uses `MeTTaOptions`
- `Setup/AgentBootstrapper.cs` — uses `Ouroboros.Options`
- `Setup/GuidedSetup.cs` — uses `Ouroboros.Options`
- `Setup/DependencyChecker.cs` — uses `Ouroboros.Options`
- `Subsystems/ModelSubsystem.cs` — uses `Ouroboros.Options`
- `GlobalUsings.cs` — may have `using Ouroboros.Options`

These must be updated to use the new config records/types before the Options folder can be deleted.

### 5b. Delete the entire legacy Options/ folder
`rm -rf src/Ouroboros.CLI/Options/`

### 5c. Remove CommandLineParser NuGet dependency
Remove from `Ouroboros.CLI.csproj`:
```xml
<PackageReference Include="CommandLineParser" Version="2.9.1" />
```

### 5d. Clean up remaining `using Ouroboros.Options;` imports
Search and remove all `using Ouroboros.Options;` statements (including from `GlobalUsings.cs`).

### 5e. Build verification
`dotnet build` — 0 errors, 0 warnings.

---

## Phase 6 — Final Verification Checklist

After all phases, confirm:

- [ ] `dotnet build` succeeds with 0 errors, 0 warnings
- [ ] Every command in Program.cs follows: `CreateXCommand()` → `Options.AddToCommand()` → `command.ConfigureXCommand()`
- [ ] Every handler lives in namespace `Ouroboros.CLI.Commands.Handlers`
- [ ] Every handler implements `ICommandHandler<TConfig>` or `ICommandHandler` (parameterless)
- [ ] Every command has a corresponding service interface (`IXService`) and implementation (`XService`) in Services/
- [ ] No `Console.WriteLine` calls remain in any actively-routed code
- [ ] No files in the legacy `Options/` folder remain
- [ ] Only ONE voice service exists (V2, renamed to VoiceModeService)
- [ ] `LocalTts` default is `false` across all config records
- [ ] No unused parameters (like `globalVoiceOption` passed but ignored)
- [ ] `MIGRATION_PLAN.md` updated to reflect 100% completion

---

## Implementation Order & Dependencies

```
Phase 1 (Delete dead code) — no dependencies, safe first step
    ↓
Phase 2 (Fix MeTTa) — depends on Phase 1c (remove AddMeTTaCommandHandler)
    ↓
Phase 3 (Unify voice) — independent of Phase 2, but logical ordering
    ↓
Phase 4 (Spectre.Console migration) — large phase, can overlap with Phase 3
    ↓
Phase 5 (Delete Options/) — depends on Phase 2 (MeTTa no longer uses MeTTaOptions)
                             depends on Phase 4 (remaining Ouroboros.Options refs updated)
    ↓
Phase 6 (Final verification) — depends on all prior phases
```

Each phase ends with `dotnet build` verification. Commit after each phase.

---

## Risk Notes

1. **Phase 4b/4c (Console.WriteLine replacement)** is the largest task by file count. The OuroborosAgent partial classes and 11 subsystems together contain ~300+ Console.WriteLine calls. This is mechanical but tedious.

2. **Phase 5a (Ouroboros.Options references in service classes)** — Some service implementations (`ImmersiveModeService`, `PipelineService`, `OrchestratorService`) import `Ouroboros.Options`. These must be updated to use the new config types before deleting the Options folder. This may require introducing new config records or adjusting service method signatures.

3. **Phase 3a (Voice service consolidation)** — The `VoiceModeExtensions.CreateVoiceService()` factory method creates V1 service instances. This must be updated to create the new unified service or removed.

4. **ChatCommand REPL input** — Uses raw `Console.ReadKey()` for terminal input handling. This is correct (Spectre.Console doesn't have a raw key-event API). These calls should not be migrated.

# Implementation Plan: Integrate OpenClaw Tools into Ouroboros

## Strategic Summary

Integrate OpenClaw as a tool set within Ouroboros, giving the reasoning engine a **communication nervous system**. The agent can reason through a complex pipeline, then use `openclaw_send_message` to deliver results via WhatsApp — or use `openclaw_node_invoke` to take a photo on a paired phone and feed it back into a vision pipeline.

**Zero changes to the existing tool system.** Pure additive integration.

---

## Context & Architecture Analysis

### Ouroboros Tool System (Existing)

- **`ITool` interface** (from `Ouroboros.Tools` project): `Name`, `Description`, `JsonSchema?`, `InvokeAsync(string, CancellationToken) → Result<string, string>`
- **`ToolRegistry`**: Immutable; `WithTool()` returns a new instance
- **Pattern**: Static class with `GetAllTools()` + extension method `With*Tools()` on `ToolRegistry`
  - `AutonomousTools.cs:55` → `.WithAutonomousTools()`
  - `GitReflectionTools.cs:59` → `.WithGitReflectionTools()`
  - `PipelineToolExtensions.cs:19` → `.WithPipelineSteps()`
- **Registration site**: `ToolSubsystem.InitializeAsync()` in `src/Ouroboros.CLI/Subsystems/ToolSubsystem.cs:76`
- **Config**: `OuroborosConfig` record (`src/Ouroboros.CLI/Commands/Ouroboros/OuroborosConfig.cs`) + `appsettings.json`
- **Shared state pattern**: Static properties (e.g., `SystemAccessTools.SharedIndexer`, `AutonomousTools.SharedCoordinator`)
- **Permission gating**: `SensitiveTools` HashSet in `ToolSubsystem.cs:858` — tools listed here require user approval via Crush-style UI

### OpenClaw Gateway API (Target)

- **Transport**: WebSocket at `ws://127.0.0.1:18789` (configurable via `gateway.port`)
- **Protocol**: JSON text frames, typed req/res/event with mandatory `type` field, validated via TypeBox schemas
- **Auth**: Token-based (`connect.params.auth.token`) or password-based; loopback auto-approves by default
- **Client roles**: `operator` (CLI/UI/automation) or `node` (capability host)
- **Key RPC methods**:
  - `connect` → handshake with role + auth
  - `chat.send` → send message to a channel/session
  - `node.list` → list connected device nodes
  - `node.describe` → get node capabilities
  - `node.invoke` → execute action on device node (camera.snap, sms.send, screen.record, location.get, etc.)
  - `node.invoke.result` → receive result from node
  - `status` → gateway health/status
  - `channels` → list active messaging channels
  - `sessions` → session management
  - `exec.approval.requested` / `exec.approval.resolve` → approval workflow
- **Channels (15+)**: WhatsApp, Telegram, Slack, Discord, Signal, iMessage/BlueBubbles, Google Chat, Teams, Matrix, Zalo, WebChat, macOS, iOS, Android
- **Existing .NET references**: [OpenClaw.NET](https://github.com/clawdotnet/openclaw.net) (NativeAOT gateway port), [OpenClaw.Shared](https://github.com/shanselman/openclaw-windows-hub) (Windows client library)

---

## Implementation Steps

### Step 1: Add OpenClaw config to `OuroborosConfig`

**File**: `src/Ouroboros.CLI/Commands/Ouroboros/OuroborosConfig.cs`

Add three new parameters to the record:
```csharp
// OpenClaw Gateway integration
string? OpenClawGateway = null,        // e.g. "ws://127.0.0.1:18789"
string? OpenClawToken = null,          // gateway auth token
bool EnableOpenClaw = false            // feature toggle (disabled by default)
```

**File**: `appsettings.json`

Add to the `Ouroboros` section:
```json
"OpenClaw": {
  "GatewayUrl": "ws://127.0.0.1:18789",
  "Token": ""
}
```

### Step 2: Create the Gateway WebSocket client

**New file**: `src/Ouroboros.Application/OpenClaw/OpenClawGatewayClient.cs` (~200 lines)

A thin, async-safe WebSocket client wrapping `System.Net.WebSockets.ClientWebSocket`:

```csharp
public sealed class OpenClawGatewayClient : IAsyncDisposable
{
    // Core connection
    Task ConnectAsync(Uri gatewayUri, string? token, CancellationToken ct)
    Task DisconnectAsync()
    bool IsConnected { get; }

    // RPC interface
    Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)

    // Lifecycle
    ValueTask DisposeAsync()
}
```

**Design details:**
- Uses `System.Net.WebSockets.ClientWebSocket` (built into .NET, no extra NuGet)
- JSON serialization via `System.Text.Json` (already used throughout Ouroboros)
- `ConcurrentDictionary<string, TaskCompletionSource<JsonElement>>` for req/res correlation by ID
- Background receive loop parsing incoming JSON frames
- `SemaphoreSlim` for send serialization (WebSocket is not thread-safe for concurrent sends)
- Auto-reconnect with exponential backoff on disconnect (configurable)
- Connect sends `type: "req", method: "connect"` with `role: "operator"` and auth token
- Implements `IAsyncDisposable` for clean shutdown

### Step 3: Create `OpenClawTools.cs`

**New file**: `src/Ouroboros.Application/Tools/OpenClawTools.cs` (~250 lines)

Static class following the established pattern exactly:

```csharp
namespace Ouroboros.Application.Tools;

public static class OpenClawTools
{
    public static OpenClawGatewayClient? SharedClient { get; set; }

    public static IEnumerable<ITool> GetAllTools()
    {
        yield return new OpenClawSendMessageTool();
        yield return new OpenClawListChannelsTool();
        yield return new OpenClawNodeInvokeTool();
        yield return new OpenClawNodeListTool();
        yield return new OpenClawStatusTool();
    }

    public static ToolRegistry WithOpenClawTools(this ToolRegistry registry)
    {
        foreach (var tool in GetAllTools())
            registry = registry.WithTool(tool);
        return registry;
    }

    // ... inner tool classes below ...
}
```

#### 5 Tool Definitions

| Tool Name | Description | Gateway RPC | Input Format |
|-----------|-------------|-------------|--------------|
| `openclaw_send_message` | Send a message through any OpenClaw channel | `chat.send` | `{"channel":"whatsapp","to":"recipient","message":"text"}` |
| `openclaw_list_channels` | List active messaging channels + status | `status` / `channels` | none |
| `openclaw_node_invoke` | Execute action on a connected device node | `node.invoke` + await `node.invoke.result` | `{"node":"phone","command":"camera.snap","params":{}}` |
| `openclaw_node_list` | List connected nodes with capabilities | `node.list` | none or filter string |
| `openclaw_status` | Get gateway health and connection info | status endpoint | none |

Each tool:
- Implements `ITool` (Name, Description, JsonSchema?, InvokeAsync)
- Returns `Result<string, string>` — `Success` with formatted output, `Failure` with error message
- Checks `SharedClient != null && SharedClient.IsConnected` before calling
- Handles JSON parse errors, WebSocket errors, timeouts gracefully

### Step 4: Register tools in `ToolSubsystem`

**File**: `src/Ouroboros.CLI/Subsystems/ToolSubsystem.cs`

In `InitializeAsync()`, after existing tool registrations (after the Roslyn tools block around line 172):

```csharp
// ── OpenClaw Gateway integration ──
if (ctx.Config.EnableOpenClaw && !string.IsNullOrEmpty(ctx.Config.OpenClawGateway))
{
    try
    {
        var openClawClient = new OpenClawGatewayClient();
        await openClawClient.ConnectAsync(
            new Uri(ctx.Config.OpenClawGateway),
            ctx.Config.OpenClawToken,
            CancellationToken.None);
        OpenClawTools.SharedClient = openClawClient;
        Tools = Tools.WithOpenClawTools();
        ctx.Output.RecordInit("OpenClaw", true,
            $"gateway {ctx.Config.OpenClawGateway} (5 tools)");
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine(OuroborosTheme.Warn(
            $"  ⚠ OpenClaw: {Markup.Escape(ex.Message)}"));
        ctx.Output.RecordInit("OpenClaw", false, ex.Message);
    }
}
else
{
    ctx.Output.RecordInit("OpenClaw", false, "disabled");
}
```

In `DisposeAsync()`:
```csharp
if (OpenClawTools.SharedClient != null)
    await OpenClawTools.SharedClient.DisposeAsync();
```

### Step 5: Add sensitive tool gating

**File**: `src/Ouroboros.CLI/Subsystems/ToolSubsystem.cs`

Add to the `SensitiveTools` HashSet (line ~858):
```csharp
"openclaw_send_message",
"openclaw_node_invoke",
```

This ensures user permission is requested before sending messages or invoking device actions, matching the existing Crush-style approval UI used for `capture_camera`, `smart_home`, etc.

### Step 6: Wire auto-tool execution patterns (optional enhancement)

**File**: `src/Ouroboros.CLI/Subsystems/ToolSubsystem.cs` — `TryAutoToolExecution()`

Add pattern matching for natural-language messaging requests so the agent proactively routes:
```csharp
// OpenClaw messaging requests
if (Regex.IsMatch(inputLower,
    @"\b(send|message|text|notify|tell)\b.*\b(whatsapp|telegram|discord|slack|signal|imessage)\b"))
{
    var sendTool = Tools.All.FirstOrDefault(t => t.Name == "openclaw_send_message");
    if (sendTool != null)
    {
        // ... auto-execution logic matching smart_home pattern
    }
}
```

### Step 7: Write unit tests

**New file**: `tests/Ouroboros.Application.Tests/Tools/OpenClawToolsTests.cs` (~100 lines)

- Test `WithOpenClawTools()` adds 5 tools to registry
- Test each tool returns `Failure` gracefully when `SharedClient` is null
- Test `OpenClawGatewayClient` connection handling with mock WebSocket

---

## File Summary

| File | Action | Est. Lines |
|------|--------|-----------|
| `src/Ouroboros.Application/OpenClaw/OpenClawGatewayClient.cs` | **New** | ~200 |
| `src/Ouroboros.Application/OpenClaw/OpenClawSecurityPolicy.cs` | **Created** | ~310 |
| `src/Ouroboros.Application/OpenClaw/OpenClawSecurityConfig.cs` | **Created** | ~120 |
| `src/Ouroboros.Application/OpenClaw/OpenClawAuditLog.cs` | **Created** | ~180 |
| `src/Ouroboros.Application/Tools/OpenClawTools.cs` | **New** | ~250 |
| `src/Ouroboros.CLI/Commands/Ouroboros/OuroborosConfig.cs` | Edit | +3 lines |
| `src/Ouroboros.CLI/Subsystems/ToolSubsystem.cs` | Edit | +30 lines |
| `appsettings.json` | Edit | +4 lines |
| `tests/Ouroboros.Application.Tests/Tools/OpenClawToolsTests.cs` | **New** | ~100 |

**Total**: ~1160 lines of new code across 6 new files + 3 small edits to existing files.
Security layer accounts for ~610 lines (3 files, already implemented).

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────┐
│              Ouroboros Agent                      │
│                                                  │
│  Pipeline DSL → LLM → Tool Selection            │
│       │                    │                     │
│       ▼                    ▼                     │
│  ToolRegistry ───────────────────────────────    │
│  ├─ AutonomousTools (20 tools)                   │
│  ├─ SystemAccessTools (30 tools)                 │
│  ├─ PerceptionTools                              │
│  ├─ GitReflectionTools                           │
│  ├─ RoslynAnalyzerTools                          │
│  └─ OpenClawTools ◀── NEW                        │
│       ├─ openclaw_send_message                   │
│       ├─ openclaw_list_channels                  │
│       ├─ openclaw_node_invoke                    │
│       ├─ openclaw_node_list                      │
│       └─ openclaw_status                         │
│              │                                   │
│              ▼                                   │
│    OpenClawGatewayClient (WebSocket)             │
└──────────────┬───────────────────────────────────┘
               │ ws://127.0.0.1:18789
               ▼
┌──────────────────────────────────────────────────┐
│           OpenClaw Gateway                        │
│                                                  │
│  Channels:                                       │
│  ├─ WhatsApp  ├─ Telegram  ├─ Discord            │
│  ├─ Signal    ├─ iMessage  ├─ Slack              │
│  ├─ Teams     ├─ Matrix    ├─ Google Chat        │
│  ├─ BlueBubbles ├─ Zalo    ├─ WebChat            │
│                                                  │
│  Nodes (device capability hosts):                │
│  ├─ iOS/Android → camera.snap, sms.send,         │
│  │                location.get, screen.record     │
│  ├─ macOS      → system.run, system.notify        │
│  └─ Headless   → system.run, system.which         │
└──────────────────────────────────────────────────┘
```

---

## Security Layer (IMPLEMENTED)

The security layer is modeled after the existing Ouroboros patterns:
- **`ToolPermissionBroker`** — interactive Allow/Deny/Session-Allow UI (Crush-style)
- **`EthicsEnforcedGitHubMcpClient`** — decorator with audit logging
- **`EthicsMessageFilter`** — fail-closed message filtering
- **`AllowedAutonomousTools`** — config-driven allowlist

### Security Architecture

```
   LLM Tool Call (openclaw_send_message / openclaw_node_invoke)
            │
            ▼
   ┌────────────────────────────────────────┐
   │  Layer 1: ToolPermissionBroker         │  ← Interactive user approval
   │  (SensitiveTools HashSet gate)         │     [a] Allow [s] Session [d] Deny
   └────────────────┬───────────────────────┘
                    ▼
   ┌────────────────────────────────────────┐
   │  Layer 2: OpenClawSecurityPolicy       │  ← Programmatic policy enforcement
   │  ├─ Channel allowlist                  │     (fail-closed, deny by default)
   │  ├─ Recipient allowlist (per-channel)  │
   │  ├─ Node command allowlist (w/ prefix) │
   │  ├─ Sensitive data scan (regex)        │
   │  ├─ Rate limiting (global + channel)   │
   │  └─ Content length limits              │
   └────────────────┬───────────────────────┘
                    ▼
   ┌────────────────────────────────────────┐
   │  Layer 3: OpenClawAuditLog             │  ← Immutable audit trail
   │  (every allowed + denied operation)    │     Thread-safe, bounded, masked PII
   └────────────────┬───────────────────────┘
                    ▼
        OpenClawGatewayClient → ws://gateway
```

### Files (already created)

| File | Lines | Description |
|------|-------|-------------|
| `src/Ouroboros.Application/OpenClaw/OpenClawSecurityPolicy.cs` | ~310 | Policy engine: allowlists, rate limiting, sensitive data detection, SMS validation |
| `src/Ouroboros.Application/OpenClaw/OpenClawSecurityConfig.cs` | ~120 | Config record: allowlists, rate limits, content limits, factory methods |
| `src/Ouroboros.Application/OpenClaw/OpenClawAuditLog.cs` | ~180 | Bounded, thread-safe audit trail with PII masking |

### Security Controls Detail

#### 1. Channel Allowlist (fail-closed)
```csharp
// Default: empty = nothing allowed
AllowedChannels = { }  // must be explicitly configured

// Example production config:
AllowedChannels = { "telegram", "slack" }
```
Only channels in this set can receive messages. Empty set = all denied.

#### 2. Recipient Allowlist (per-channel, optional)
```csharp
// Per-channel recipient restrictions
AllowedRecipients = {
    ["whatsapp"] = { "+15551234567", "+15559876543" },
    ["sms"]      = { "+15551234567" },
}
// Channels with no entry allow any recipient (if channel itself is allowed)
```

#### 3. Node Command Allowlist (prefix wildcards)
```csharp
// Default: empty = no commands allowed
AllowedNodeCommands = { }

// Example: allow camera + location, deny system.run
AllowedNodeCommands = { "camera.*", "location.get", "canvas.*" }
```
- `system.run` is **always blocked** (classified as dangerous) regardless of allowlist
- Wildcard prefix matching: `camera.*` allows `camera.snap`, `camera.clip`

#### 4. Sensitive Data Redaction
Compiled regex patterns detect and block outbound messages containing:
- API keys/tokens (generic, AWS `AKIA*`, Bearer tokens)
- Private keys (`-----BEGIN PRIVATE KEY-----`)
- Connection strings with embedded passwords
- JWT tokens (`eyJ...`)
- Credit card numbers (Luhn-eligible patterns)
- SSN patterns (US format: `XXX-XX-XXXX`)
- Passwords in config format (`password=...`)

#### 5. Rate Limiting
- **Global**: 20 messages per 60s window (configurable)
- **Per-channel**: 10 messages per 60s window (configurable)
- Sliding window implementation with queue-based timestamp tracking
- Thread-safe via `lock`

#### 6. Audit Log
- Every operation (allowed + denied) is recorded
- Bounded to 1000 entries (FIFO eviction)
- PII masked: phone numbers → `+155****4567`, emails → `j***@example.com`
- Thread-safe via `ConcurrentQueue`
- `GetSummary()` for agent status display
- Lifetime counters: `TotalAllowed`, `TotalDenied`

### Security Config Presets

```csharp
// Production default: everything locked down
var policy = OpenClawSecurityConfig.CreateDefault();

// Development: common channels + safe node commands, scanning still active
var policy = OpenClawSecurityConfig.CreateDevelopment();
```

### Integration with Existing Security

The security layer stacks on top of the existing Ouroboros permission system:

1. **`SensitiveTools` HashSet** already gates `openclaw_send_message` and `openclaw_node_invoke` through the `ToolPermissionBroker` — user must press `[a]` Allow before the tool executes
2. **`OpenClawSecurityPolicy`** enforces programmatic rules *after* user approval — even if the user allows the tool, the policy still blocks if the channel/recipient/content violates rules
3. **`AutonomousConfig.AllowedAutonomousTools`** controls whether the autonomous mind can call OpenClaw tools without user interaction — OpenClaw tools are **not in the default allowlist**, so autonomous execution is blocked unless explicitly configured
4. **`OpenClawAuditLog`** provides post-hoc review independent of the other layers

---

## Key Design Decisions

1. **WebSocket over HTTP**: Matches OpenClaw's native protocol; enables bidirectional streaming and future event subscription
2. **Shared static client** (`OpenClawTools.SharedClient`): Follows established pattern used by `SystemAccessTools.SharedIndexer`, `AutonomousTools.SharedCoordinator`
3. **Feature-flagged** (`EnableOpenClaw = false`): Zero impact when not configured; opt-in via config
4. **Immutable ToolRegistry**: All registration via `WithOpenClawTools()` extension method — no mutation
5. **Permission-gated**: `openclaw_send_message` and `openclaw_node_invoke` added to sensitive tools for user approval before execution
6. **No changes to tool system itself**: Pure additive integration — no existing code needs modification beyond config and registration
7. **No new NuGet dependencies**: Uses built-in `System.Net.WebSockets.ClientWebSocket` and `System.Text.Json`
8. **Operator role**: Connects as `role: "operator"` for full control plane access (send messages, invoke nodes, manage sessions)

---

## Usage Scenarios After Integration

**Scenario 1: Pipeline result delivery**
```
User: "Analyze my codebase and send a summary to my Telegram"
Agent: [runs pipeline] → [uses openclaw_send_message] → delivers result via Telegram
```

**Scenario 2: Vision pipeline with phone camera**
```
User: "Take a photo with my phone and describe what you see"
Agent: [uses openclaw_node_invoke camera.snap] → receives base64 image → [feeds into vision pipeline]
```

**Scenario 3: Multi-channel notification**
```
User: "Monitor this service and alert me on WhatsApp if it goes down"
Agent: [autonomous monitoring loop] → [uses openclaw_send_message channel:whatsapp] on failure
```

**Scenario 4: Device orchestration**
```
User: "Get my phone's location and check the weather there"
Agent: [uses openclaw_node_invoke location.get] → [uses web_search with coordinates] → responds
```

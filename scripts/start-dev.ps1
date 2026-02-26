# start-dev.ps1 — Launch Ouroboros with OpenClaw PC Node (dev environment)
#
# Usage:
#   .\scripts\start-dev.ps1                  # default: ouroboros mode
#   .\scripts\start-dev.ps1 -Mode immersive  # immersive mode
#   .\scripts\start-dev.ps1 -SkipGateway     # skip gateway check/start
#   .\scripts\start-dev.ps1 -ExtraArgs "--voice --avatar"

param(
    [ValidateSet("ouroboros", "immersive", "chat")]
    [string]$Mode = "ouroboros",

    [switch]$SkipGateway,

    [string]$ExtraArgs = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$CliProject = Join-Path $ProjectRoot "src\Ouroboros.CLI"
$PcNodeConfig = Join-Path $ProjectRoot "config\pcnode-dev.json"
$GatewayPort = 18789
$GatewayUrl = "ws://127.0.0.1:$GatewayPort"

Write-Host ""
Write-Host "=== Ouroboros + OpenClaw Dev Environment ===" -ForegroundColor Cyan
Write-Host ""

# ── 1. Gateway health check ───────────────────────────────────────────────
if (-not $SkipGateway) {
    Write-Host "[1/3] Checking OpenClaw gateway..." -ForegroundColor Yellow

    $portInUse = netstat -ano | Select-String ":$GatewayPort\s+.*LISTENING"
    if (-not $portInUse) {
        Write-Host "  Gateway not running. Starting..." -ForegroundColor DarkYellow
        try {
            openclaw gateway start 2>&1 | Out-Null
            Start-Sleep -Seconds 3
            $portInUse = netstat -ano | Select-String ":$GatewayPort\s+.*LISTENING"
            if ($portInUse) {
                Write-Host "  Gateway started on port $GatewayPort" -ForegroundColor Green
            } else {
                Write-Host "  WARNING: Gateway may not have started. Continuing anyway..." -ForegroundColor Red
            }
        } catch {
            Write-Host "  WARNING: Could not start gateway: $_" -ForegroundColor Red
            Write-Host "  Try: openclaw gateway start" -ForegroundColor DarkYellow
        }
    } else {
        Write-Host "  Gateway already running on port $GatewayPort" -ForegroundColor Green
    }

    # RPC probe
    try {
        $probeOutput = openclaw gateway status 2>&1 | Out-String
        if ($probeOutput -match "RPC probe: ok") {
            Write-Host "  RPC probe: OK" -ForegroundColor Green
        } else {
            Write-Host "  RPC probe: WARN (gateway may need token sync)" -ForegroundColor DarkYellow
        }
    } catch {
        Write-Host "  RPC probe: skipped" -ForegroundColor DarkGray
    }
} else {
    Write-Host "[1/3] Skipping gateway check" -ForegroundColor DarkGray
}

# ── 2. Verify PC node config ──────────────────────────────────────────────
Write-Host "[2/3] Verifying PC node config..." -ForegroundColor Yellow

if (Test-Path $PcNodeConfig) {
    Write-Host "  Config: $PcNodeConfig" -ForegroundColor Green
} else {
    Write-Host "  WARNING: $PcNodeConfig not found" -ForegroundColor Red
    Write-Host "  PC node will use fail-closed defaults (all capabilities disabled)" -ForegroundColor DarkYellow
    $PcNodeConfig = $null
}

# ── 3. Launch Ouroboros CLI ───────────────────────────────────────────────
Write-Host "[3/3] Launching Ouroboros ($Mode mode)..." -ForegroundColor Yellow
Write-Host ""

$env:DOTNET_ENVIRONMENT = "Development"

$args = @($Mode, "--enable-openclaw", "--enable-pc-node")

if ($PcNodeConfig) {
    $args += "--pc-node-config"
    $args += $PcNodeConfig
}

$args += "--openclaw-gateway"
$args += $GatewayUrl

if ($ExtraArgs) {
    $args += $ExtraArgs.Split(" ", [StringSplitOptions]::RemoveEmptyEntries)
}

Write-Host "  dotnet run --project $CliProject -- $($args -join ' ')" -ForegroundColor DarkGray
Write-Host ""

dotnet run --project $CliProject -- @args

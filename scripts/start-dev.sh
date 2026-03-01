#!/usr/bin/env bash
# start-dev.sh — Launch Ouroboros with OpenClaw PC Node (dev environment)
#
# Usage:
#   ./scripts/start-dev.sh                       # default: ouroboros mode
#   ./scripts/start-dev.sh immersive             # immersive mode
#   ./scripts/start-dev.sh ouroboros --voice      # pass extra args

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
CLI_PROJECT="$PROJECT_ROOT/src/Ouroboros.CLI"
PC_NODE_CONFIG="$PROJECT_ROOT/config/pcnode-dev.json"
GATEWAY_PORT=18789
GATEWAY_URL="ws://127.0.0.1:$GATEWAY_PORT"

MODE="${1:-ouroboros}"
shift 2>/dev/null || true

echo ""
echo "=== Ouroboros + OpenClaw Dev Environment ==="
echo ""

# ── 1. Gateway health check ──────────────────────────────────────────────
echo "[1/3] Checking OpenClaw gateway..."

if netstat -ano 2>/dev/null | grep -q ":$GATEWAY_PORT.*LISTEN"; then
    echo "  Gateway already running on port $GATEWAY_PORT"
else
    echo "  Gateway not running. Starting..."
    openclaw gateway start 2>&1 || true
    sleep 3
    if netstat -ano 2>/dev/null | grep -q ":$GATEWAY_PORT.*LISTEN"; then
        echo "  Gateway started on port $GATEWAY_PORT"
    else
        echo "  WARNING: Gateway may not have started. Try: openclaw gateway start"
    fi
fi

# ── 2. Verify PC node config ─────────────────────────────────────────────
echo "[2/3] Verifying PC node config..."

PC_NODE_ARGS=""
if [ -f "$PC_NODE_CONFIG" ]; then
    echo "  Config: $PC_NODE_CONFIG"
    PC_NODE_ARGS="--pc-node-config $PC_NODE_CONFIG"
else
    echo "  WARNING: $PC_NODE_CONFIG not found"
    echo "  PC node will use fail-closed defaults (all capabilities disabled)"
fi

# ── 3. Launch Ouroboros CLI ───────────────────────────────────────────────
echo "[3/3] Launching Ouroboros ($MODE mode)..."
echo ""

export DOTNET_ENVIRONMENT=Development

CMD="dotnet run -c Release --project $CLI_PROJECT -- $MODE --enable-openclaw --enable-pc-node $PC_NODE_ARGS --openclaw-gateway $GATEWAY_URL $*"
echo "  $CMD"
echo ""

exec $CMD

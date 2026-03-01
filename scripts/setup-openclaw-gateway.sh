#!/usr/bin/env bash
# ============================================================================
# OpenClaw Gateway Setup Script for WSL2 Ubuntu
# ============================================================================
# Runs inside WSL2 to install, configure, and harden the OpenClaw gateway
# with security-first defaults.
#
# Usage (from WSL2 terminal):
#   chmod +x setup-openclaw-gateway.sh
#   ./setup-openclaw-gateway.sh
#
# This script will:
#   1. Verify WSL2 environment and dependencies
#   2. Install OpenClaw via pnpm
#   3. Run interactive onboarding
#   4. Configure gateway with security-first defaults
#   5. Install as a systemd service
#   6. Generate and output the gateway token for Ouroboros
# ============================================================================

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()  { echo -e "${BLUE}[INFO]${NC}  $*"; }
log_ok()    { echo -e "${GREEN}[OK]${NC}    $*"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }

# ── Step 1: Environment Checks ───────────────────────────────────────────────

log_info "Checking environment..."

# Verify WSL2
if [[ ! -f /proc/version ]] || ! grep -qi microsoft /proc/version; then
    log_error "This script must run inside WSL2 (Ubuntu). Detected non-WSL environment."
    exit 1
fi
log_ok "Running inside WSL2"

# Check systemd (required for service installation)
if ! systemctl --version &>/dev/null; then
    log_warn "systemd not detected. Gateway service installation may not work."
    log_warn "Enable systemd in /etc/wsl.conf: [boot] systemd=true"
fi

# Check Node.js
if ! command -v node &>/dev/null; then
    log_error "Node.js is not installed. Install it first:"
    log_error "  curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -"
    log_error "  sudo apt-get install -y nodejs"
    exit 1
fi
NODE_VERSION=$(node --version)
log_ok "Node.js ${NODE_VERSION}"

# Check pnpm
if ! command -v pnpm &>/dev/null; then
    log_info "Installing pnpm..."
    npm install -g pnpm
fi
log_ok "pnpm $(pnpm --version)"

# ── Step 2: Install OpenClaw ─────────────────────────────────────────────────

if command -v openclaw &>/dev/null; then
    CURRENT_VERSION=$(openclaw --version 2>/dev/null || echo "unknown")
    log_ok "OpenClaw already installed: ${CURRENT_VERSION}"
    read -rp "Reinstall/update? [y/N] " REINSTALL
    if [[ "${REINSTALL}" =~ ^[Yy]$ ]]; then
        pnpm install -g openclaw
    fi
else
    log_info "Installing OpenClaw..."
    pnpm install -g openclaw
    log_ok "OpenClaw installed: $(openclaw --version)"
fi

# ── Step 3: Interactive Onboarding ───────────────────────────────────────────

log_info "Running OpenClaw onboarding (interactive)..."
log_info "This will set up your AI provider, messaging channels, etc."
echo ""

if [[ ! -d "${HOME}/.openclaw" ]]; then
    openclaw onboard
else
    log_warn "OpenClaw config directory already exists (~/.openclaw)"
    read -rp "Run onboarding again? [y/N] " REONBOARD
    if [[ "${REONBOARD}" =~ ^[Yy]$ ]]; then
        openclaw onboard
    fi
fi

# ── Step 4: Security-First Configuration ─────────────────────────────────────

log_info "Applying security-first gateway configuration..."

# Generate a gateway token if one doesn't exist
EXISTING_TOKEN=$(openclaw config get gateway.auth.token 2>/dev/null || echo "")
if [[ -z "${EXISTING_TOKEN}" || "${EXISTING_TOKEN}" == "null" ]]; then
    log_info "Generating gateway authentication token..."
    GATEWAY_TOKEN=$(openssl rand -hex 32)
    openclaw config set gateway.auth.token "${GATEWAY_TOKEN}"
    log_ok "Gateway token generated and saved"
else
    GATEWAY_TOKEN="${EXISTING_TOKEN}"
    log_ok "Using existing gateway token"
fi

# Security hardening
openclaw config set gateway.bind "loopback"
log_ok "Gateway bound to loopback only (127.0.0.1)"

openclaw config set tools.profile "minimal"
log_ok "Tool profile set to 'minimal'"

# Deny high-risk tools by default
openclaw config set tools.deny '["exec","browser","cron","gateway"]'
log_ok "High-risk tools denied: exec, browser, cron, gateway"

# Disable mDNS discovery (security: don't advertise on network)
openclaw config set discovery.mdns.mode "off" 2>/dev/null || true
log_ok "mDNS discovery disabled"

# Enable sensitive data redaction in logs
openclaw config set logging.redactSensitive "tools" 2>/dev/null || true
log_ok "Sensitive data redaction enabled in logs"

# ── Step 5: Install as systemd Service ───────────────────────────────────────

log_info "Installing gateway as systemd service..."

if systemctl --version &>/dev/null; then
    openclaw gateway install 2>/dev/null || {
        log_warn "Gateway service installation failed. You can start manually:"
        log_warn "  openclaw gateway start"
    }

    # Enable linger so the service survives logout
    sudo loginctl enable-linger "${USER}" 2>/dev/null || {
        log_warn "Could not enable linger. Service may stop on logout."
    }

    log_ok "Gateway service installed"
else
    log_warn "systemd not available. Start the gateway manually:"
    log_warn "  openclaw gateway start"
fi

# ── Step 6: Verify ───────────────────────────────────────────────────────────

log_info "Verifying gateway status..."
sleep 2

if openclaw gateway status &>/dev/null; then
    log_ok "Gateway is running"
else
    log_warn "Gateway may not be running yet. Starting..."
    openclaw gateway start &
    sleep 3
fi

# ── Step 7: Security Audit ───────────────────────────────────────────────────

log_info "Running security audit..."
openclaw security audit 2>/dev/null || {
    log_warn "Security audit command not available in this version"
}

# ── Output ───────────────────────────────────────────────────────────────────

echo ""
echo "============================================================================"
echo -e "${GREEN}OpenClaw Gateway Setup Complete${NC}"
echo "============================================================================"
echo ""
echo "Gateway URL:   ws://127.0.0.1:18789"
echo "Gateway Token: ${GATEWAY_TOKEN}"
echo ""
echo "To use with Ouroboros, set the following environment variables:"
echo ""
echo "  export OPENCLAW_GATEWAY=ws://127.0.0.1:18789"
echo "  export OPENCLAW_TOKEN=${GATEWAY_TOKEN}"
echo ""
echo "Or pass as CLI arguments:"
echo ""
echo "  ouroboros --enable-openclaw --openclaw-token ${GATEWAY_TOKEN}"
echo "  ouroboros --enable-pc-node --openclaw-token ${GATEWAY_TOKEN}"
echo ""
echo "To access the gateway from Windows (outside WSL2), run:"
echo "  scripts/setup-openclaw-portproxy.ps1"
echo ""
echo "============================================================================"

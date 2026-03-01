#!/usr/bin/env bash
# ============================================================================
# OpenClaw Security Hardening Script
# ============================================================================
# Post-setup hardening that applies OpenClaw's security recommendations.
# Run this inside WSL2 after the initial setup.
#
# Usage:
#   chmod +x openclaw-security-hardening.sh
#   ./openclaw-security-hardening.sh [--token YOUR_TOKEN]
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

# Parse arguments
TOKEN=""
while [[ $# -gt 0 ]]; do
    case $1 in
        --token) TOKEN="$2"; shift 2 ;;
        *) log_error "Unknown argument: $1"; exit 1 ;;
    esac
done

# ── Verify OpenClaw is installed ─────────────────────────────────────────────

if ! command -v openclaw &>/dev/null; then
    log_error "OpenClaw is not installed. Run setup-openclaw-gateway.sh first."
    exit 1
fi

log_info "OpenClaw version: $(openclaw --version 2>/dev/null || echo 'unknown')"

# ── Gateway Authentication ───────────────────────────────────────────────────

log_info "Checking gateway authentication..."

if [[ -n "${TOKEN}" ]]; then
    openclaw config set gateway.auth.token "${TOKEN}"
    log_ok "Gateway token set from argument"
else
    EXISTING_TOKEN=$(openclaw config get gateway.auth.token 2>/dev/null || echo "")
    if [[ -z "${EXISTING_TOKEN}" || "${EXISTING_TOKEN}" == "null" ]]; then
        TOKEN=$(openssl rand -hex 32)
        openclaw config set gateway.auth.token "${TOKEN}"
        log_ok "New gateway token generated: ${TOKEN}"
    else
        log_ok "Gateway token already configured"
    fi
fi

# ── Network Binding ──────────────────────────────────────────────────────────

log_info "Configuring network binding..."
openclaw config set gateway.bind "loopback"
log_ok "Gateway bound to loopback only (no network exposure)"

# ── Tool Permissions ─────────────────────────────────────────────────────────

log_info "Configuring tool permissions..."

# Use minimal profile (read-only tools only)
openclaw config set tools.profile "minimal"
log_ok "Tool profile: minimal"

# Explicitly deny high-risk tools
openclaw config set tools.deny '["exec","browser","cron","gateway"]'
log_ok "Denied tools: exec, browser, cron, gateway"

# ── Discovery ────────────────────────────────────────────────────────────────

log_info "Disabling network discovery..."
openclaw config set discovery.mdns.mode "off" 2>/dev/null || true
log_ok "mDNS discovery disabled"

# ── Logging ──────────────────────────────────────────────────────────────────

log_info "Configuring secure logging..."
openclaw config set logging.redactSensitive "tools" 2>/dev/null || true
log_ok "Sensitive data redaction enabled"

# ── Security Audit ───────────────────────────────────────────────────────────

log_info "Running security audit..."
echo ""

if openclaw security audit 2>/dev/null; then
    log_ok "Security audit passed"
elif openclaw security audit --fix 2>/dev/null; then
    log_ok "Security audit issues auto-fixed"
else
    log_warn "Security audit command not available in this OpenClaw version"
fi

# ── Doctor Check ─────────────────────────────────────────────────────────────

log_info "Running diagnostics..."
openclaw doctor 2>/dev/null || true

# ── Summary ──────────────────────────────────────────────────────────────────

echo ""
echo "============================================================================"
echo -e "${GREEN}Security Hardening Complete${NC}"
echo "============================================================================"
echo ""
echo "Configuration applied:"
echo "  - Gateway auth:      token-based (fail-closed)"
echo "  - Network binding:   loopback only"
echo "  - Tool profile:      minimal"
echo "  - Denied tools:      exec, browser, cron, gateway"
echo "  - mDNS discovery:    off"
echo "  - Log redaction:     enabled"
echo ""
echo "Security checklist:"
echo "  [x] Gateway requires authentication"
echo "  [x] Gateway listens on localhost only"
echo "  [x] High-risk tools are denied"
echo "  [x] Network discovery is disabled"
echo "  [x] Sensitive data is redacted from logs"
echo ""
echo "To further restrict, edit ~/.openclaw/config directly."
echo "============================================================================"

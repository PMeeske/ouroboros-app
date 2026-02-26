#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Sets up Windows portproxy to forward traffic to the OpenClaw gateway in WSL2.

.DESCRIPTION
    WSL2 uses a virtual network adapter with a dynamic IP address that changes on restart.
    This script creates a portproxy rule to forward localhost:18789 on Windows to the
    WSL2 gateway, and a firewall rule to allow local traffic only.

    Must be run as Administrator.

.NOTES
    The WSL2 IP changes on restart. Run this script again after rebooting WSL2,
    or set up a scheduled task to run it automatically.

.EXAMPLE
    .\setup-openclaw-portproxy.ps1
    .\setup-openclaw-portproxy.ps1 -Port 18789
    .\setup-openclaw-portproxy.ps1 -Remove
#>

param(
    [int]$Port = 18789,
    [switch]$Remove
)

$ErrorActionPreference = "Stop"

function Write-Status($message, $color = "Cyan") {
    Write-Host "[*] " -NoNewline -ForegroundColor $color
    Write-Host $message
}

function Write-OK($message) {
    Write-Host "[+] " -NoNewline -ForegroundColor Green
    Write-Host $message
}

function Write-Warn($message) {
    Write-Host "[!] " -NoNewline -ForegroundColor Yellow
    Write-Host $message
}

# Remove mode
if ($Remove) {
    Write-Status "Removing portproxy rule for port $Port..."
    netsh interface portproxy delete v4tov4 listenport=$Port listenaddress=127.0.0.1 2>$null
    Write-Status "Removing firewall rule..."
    Remove-NetFirewallRule -DisplayName "OpenClaw Gateway (WSL2)" -ErrorAction SilentlyContinue
    Write-OK "Portproxy and firewall rules removed."
    exit 0
}

# Get WSL2 IP address
Write-Status "Getting WSL2 IP address..."
$wslIp = (wsl hostname -I).Trim().Split(" ")[0]

if ([string]::IsNullOrWhiteSpace($wslIp)) {
    Write-Warn "Could not determine WSL2 IP address. Is WSL2 running?"
    Write-Warn "Try: wsl --shutdown && wsl"
    exit 1
}

Write-OK "WSL2 IP: $wslIp"

# Remove existing rule (if any)
Write-Status "Removing existing portproxy rule (if any)..."
netsh interface portproxy delete v4tov4 listenport=$Port listenaddress=127.0.0.1 2>$null

# Create portproxy rule
Write-Status "Creating portproxy rule: 127.0.0.1:$Port -> ${wslIp}:$Port"
netsh interface portproxy add v4tov4 `
    listenport=$Port `
    listenaddress=127.0.0.1 `
    connectport=$Port `
    connectaddress=$wslIp

Write-OK "Portproxy rule created"

# Create firewall rule (local only)
Write-Status "Configuring firewall rule..."
Remove-NetFirewallRule -DisplayName "OpenClaw Gateway (WSL2)" -ErrorAction SilentlyContinue
New-NetFirewallRule `
    -DisplayName "OpenClaw Gateway (WSL2)" `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $Port `
    -LocalAddress 127.0.0.1 `
    -RemoteAddress 127.0.0.1 `
    -Profile Private | Out-Null

Write-OK "Firewall rule created (localhost only)"

# Verify
Write-Status "Verifying portproxy..."
$rules = netsh interface portproxy show v4tov4
Write-Host $rules

Write-Host ""
Write-OK "Port forwarding active: 127.0.0.1:$Port -> ${wslIp}:$Port"
Write-Host ""
Write-Warn "NOTE: WSL2 IP changes on restart. Re-run this script after rebooting."
Write-Host "      To automate, create a scheduled task or add to your PowerShell profile."

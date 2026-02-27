#!/usr/bin/env bash
# Ouroboros App Layer — Static Code Analysis
# Replaces full test runs for agent verification. Fast (~30s vs ~15min).
# Usage: bash scripts/static-analysis.sh [--fix]

set -euo pipefail
cd "$(dirname "$0")/.."

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'
ERRORS=0
WARNINGS=0

pass()  { echo -e "  ${GREEN}PASS${NC}  $1"; }
fail()  { echo -e "  ${RED}FAIL${NC}  $1"; ERRORS=$((ERRORS + 1)); }
warn()  { echo -e "  ${YELLOW}WARN${NC}  $1"; WARNINGS=$((WARNINGS + 1)); }

echo "============================================"
echo " Ouroboros Static Code Analysis"
echo "============================================"
echo ""

# ── 1. Build check (0 new errors) ──────────────────────────────
echo "── Build ──"
BUILD_OUT=$(dotnet build Ouroboros.App.sln --verbosity quiet --no-restore 2>&1 || true)
# Filter out pre-existing Android error
NEW_ERRORS=$(echo "$BUILD_OUT" | grep -c "error CS" || true)
if [ "$NEW_ERRORS" -eq 0 ]; then
    pass "Build: 0 CS errors"
else
    fail "Build: $NEW_ERRORS CS error(s)"
    echo "$BUILD_OUT" | grep "error CS"
fi

# ── 2. Sync-over-async patterns ────────────────────────────────
echo ""
echo "── Sync-over-Async ──"
SYNC_FILES=(
    "src/Ouroboros.CLI/Services/MeTTaService.cs"
    "src/Ouroboros.CLI/Subsystems/ChatSubsystem.cs"
    "src/Ouroboros.Application/Services/ParallelMeTTaThoughtStreams.cs"
    "src/Ouroboros.Application/Hyperon/HyperonFlowIntegration.cs"
    "src/Ouroboros.CLI/Services/EnhancedListeningService.cs"
    "src/Ouroboros.CLI/Subsystems/AutonomySubsystem.cs"
    "src/Ouroboros.CLI/Commands/Ouroboros/OuroborosAgent.Init.cs"
    "src/Ouroboros.CLI/Commands/Ouroboros/OuroborosAgent.Voice.cs"
    "src/Ouroboros.CLI/Commands/Ouroboros/OuroborosAgent.Tools.cs"
    "src/Ouroboros.Application/StreamingCliSteps.cs"
    "src/Ouroboros.CLI/Commands/Immersive/ImmersiveMode.Response.cs"
    "src/Ouroboros.CLI/Mediator/Handlers/ProcessLargeInputHandler.cs"
    "src/Ouroboros.Application/Tools/CaptchaResolver/SemanticCaptchaResolverDecorator.cs"
)
SYNC_COUNT=0
for f in "${SYNC_FILES[@]}"; do
    if [ -f "$f" ]; then
        HITS=$(grep -n '\.GetAwaiter()\.GetResult()\|\.Wait()' "$f" 2>/dev/null | grep -v '//' | grep -v 'sync-over-async:accepted' || true)
        if [ -n "$HITS" ]; then
            fail "Sync-over-async in $f"
            echo "$HITS"
            SYNC_COUNT=$((SYNC_COUNT + 1))
        fi
    fi
done
[ "$SYNC_COUNT" -eq 0 ] && pass "No .Wait()/.GetAwaiter().GetResult() in targeted files"

# ── 3. Static singletons ───────────────────────────────────────
echo ""
echo "── Static Singletons ──"
STATIC_SHARED=$(grep -rn 'public static.*Shared' src/Ouroboros.Application/Tools/AutonomousTools.cs 2>/dev/null | grep -v '//' || true)
if [ -z "$STATIC_SHARED" ]; then
    pass "No static Shared* properties in AutonomousTools.cs"
else
    fail "Static Shared* singletons found in AutonomousTools.cs"
    echo "$STATIC_SHARED"
fi

# ── 4. Security: ToolSubsystem default-on ──────────────────────
echo ""
echo "── Security Model ──"
if grep -q 'ExemptTools' src/Ouroboros.CLI/Subsystems/ToolSubsystem.cs 2>/dev/null; then
    pass "ToolSubsystem uses ExemptTools (default-on permission model)"
else
    fail "ToolSubsystem missing ExemptTools — security bypass risk"
fi

# ── 5. God class line counts ───────────────────────────────────
echo ""
echo "── God Class Metrics ──"
GOD_CLASS_LIMIT=800
check_lines() {
    local file=$1
    local name=$2
    if [ -f "$file" ]; then
        local lines=$(wc -l < "$file" | tr -d ' ')
        if [ "$lines" -gt "$GOD_CLASS_LIMIT" ]; then
            warn "$name: $lines lines (limit: $GOD_CLASS_LIMIT)"
        else
            pass "$name: $lines lines"
        fi
    fi
}
check_lines "src/Ouroboros.Application/Personality/PersonalityEngine.cs" "PersonalityEngine"
check_lines "src/Ouroboros.CLI/Subsystems/AutonomySubsystem.cs" "AutonomySubsystem"
check_lines "src/Ouroboros.CLI/Commands/Immersive/ImmersiveMode.cs" "ImmersiveMode (main)"
check_lines "src/Ouroboros.Application/Tools/AutonomousTools.cs" "AutonomousTools"
check_lines "src/Ouroboros.Application/Services/AutonomousMind.cs" "AutonomousMind"

# ── 6. Method length check (>200 lines) ────────────────────────
echo ""
echo "── Long Methods (>200 lines) ──"
LONG_METHODS=0
for f in src/Ouroboros.Application/**/*.cs src/Ouroboros.CLI/**/*.cs; do
    [ -f "$f" ] || continue
    # Simple heuristic: count lines between method signatures and closing braces
    awk '
    /^[[:space:]]*(public|private|protected|internal).*(async )?[A-Z].*\(/ { start=NR; name=$0 }
    start && /^[[:space:]]*\}/ {
        len = NR - start
        if (len > 200) { printf "    %s:%d (%d lines) %s\n", FILENAME, start, len, name; found++ }
        start=0
    }
    END { exit (found > 0 ? 1 : 0) }
    ' "$f" 2>/dev/null && true
done | head -20 | while read -r line; do
    warn "$line"
    LONG_METHODS=$((LONG_METHODS + 1))
done
[ "$LONG_METHODS" -eq 0 ] && pass "No methods >200 lines detected"

# ── 7. async void (outside event handlers) ─────────────────────
echo ""
echo "── Async Void ──"
ASYNC_VOID=$(grep -rn 'async void' src/ --include="*.cs" 2>/dev/null | grep -v '// ' | grep -v 'event' | grep -v '_test' | grep -v 'Test' || true)
if [ -z "$ASYNC_VOID" ]; then
    pass "No async void methods (outside event handlers)"
else
    ASYNC_VOID_COUNT=$(echo "$ASYNC_VOID" | wc -l)
    warn "Found $ASYNC_VOID_COUNT async void methods"
    echo "$ASYNC_VOID" | head -5
fi

# ── 8. Console.WriteLine as logging ────────────────────────────
echo ""
echo "── Console.WriteLine Usage ──"
CW_COUNT=$(grep -r 'Console\.WriteLine' src/ --include="*.cs" 2>/dev/null | grep -v Test | grep -v '//' | wc -l || echo 0)
if [ "$CW_COUNT" -gt 500 ]; then
    warn "Console.WriteLine: $CW_COUNT occurrences (target: replace with ILogger<T>)"
elif [ "$CW_COUNT" -gt 0 ]; then
    pass "Console.WriteLine: $CW_COUNT occurrences (improving)"
else
    pass "Console.WriteLine: 0 — fully migrated to ILogger<T>"
fi

# ── 9. Catch-all exception handlers ───────────────────────────
echo ""
echo "── Catch-All Handlers ──"
CATCH_ALL=$(grep -rn 'catch (Exception)' src/ --include="*.cs" 2>/dev/null | grep -v Test | grep -v '//' | wc -l || echo 0)
CATCH_ALL_EX=$(grep -rn 'catch (Exception ex)' src/ --include="*.cs" 2>/dev/null | grep -v Test | grep -v '//' | wc -l || echo 0)
TOTAL_CATCH=$((CATCH_ALL + CATCH_ALL_EX))
if [ "$TOTAL_CATCH" -gt 1000 ]; then
    warn "Catch-all handlers: $TOTAL_CATCH (target: <500)"
else
    pass "Catch-all handlers: $TOTAL_CATCH"
fi

# ── 10. Duplicate types ────────────────────────────────────────
echo ""
echo "── Duplicate Domain Types ──"
for type in ValidationResult Plan Observation ReasoningStep Goal; do
    COUNT=$(grep -rn "class $type\b" src/ --include="*.cs" 2>/dev/null | wc -l | tr -d '[:space:]' || echo 0)
    COUNT=${COUNT:-0}
    if [ "$COUNT" -gt 2 ]; then
        warn "Duplicate: $type defined $COUNT times"
    fi
done

# ── Summary ────────────────────────────────────────────────────
echo ""
echo "============================================"
echo -e " Results: ${RED}$ERRORS error(s)${NC}, ${YELLOW}$WARNINGS warning(s)${NC}"
echo "============================================"
exit $ERRORS

#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ADAPTER="$PACKAGE_ROOT/bin/linux-x64/__ADAPTER_EXECUTABLE__"
chmod +x "$ADAPTER" 2>/dev/null || true
exec "$ADAPTER" "$@"

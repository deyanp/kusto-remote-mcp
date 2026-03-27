#!/usr/bin/env bash
set -euo pipefail

# resolve to absolute path so references work after pushd changes the CWD
CURRENT_FOLDER="$(cd "$(dirname $0)" && pwd)"
PROJECT_DIR="$CURRENT_FOLDER/src"

# Simple flag parsing: --mcp true|false (default: false)
mcp="false"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --mcp) mcp="$2"; shift 2 ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

# Load environment variables from launchSettings.json (LocalDev profile)
echo "Loading environment variables from launchSettings.json..."
while IFS='=' read -r key value; do
    export "$key=$value"
    echo "  $key=$value"
done < <(jq -r '.profiles.LocalDev.environmentVariables | to_entries[] | "\(.key)=\(.value)"' "$PROJECT_DIR/Properties/launchSettings.json")
echo ""

if [ "$mcp" = "true" ]; then
    LISTEN_URL=$(jq -r '.profiles.LocalDev.applicationUrl' "$PROJECT_DIR/Properties/launchSettings.json")

    # cloudflared_tunnel.sh outputs: line 1 = tunnel URL, line 2 = cloudflared PID.
    # NOTE: if something fails between here and the trap line below, the cloudflared
    # process will be orphaned. This is a known trade-off — see cloudflared_tunnel.sh.
    TUNNEL_OUTPUT=$("$CURRENT_FOLDER/cloudflared_tunnel.sh" --url "$LISTEN_URL")
    export MCP_BASE_URL=$(echo "$TUNNEL_OUTPUT" | head -1)
    CLOUDFLARED_PID=$(echo "$TUNNEL_OUTPUT" | tail -1)
    trap "kill $CLOUDFLARED_PID 2>/dev/null" EXIT

    echo "MCP endpoint: $MCP_BASE_URL/mcp"
    echo ""
fi

dotnet run --project "$PROJECT_DIR" --launch-profile LocalDev

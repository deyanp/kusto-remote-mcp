#!/usr/bin/env bash

# Starts a cloudflared tunnel to expose a local service to the internet.
# Outputs two lines to stdout:
#   line 1: the tunnel's public URL
#   line 2: the cloudflared background process PID
#
# The caller is responsible for killing the cloudflared process when done.
# NOTE: if the caller fails between capturing the output and setting up a
# trap/cleanup, the cloudflared process will be orphaned. This is a known
# trade-off of running the script as a subprocess (vs sourcing it).
#
# Usage:
#   output=$(./cloudflared_tunnel.sh --url http://localhost:7111)
#   TUNNEL_URL=$(echo "$output" | head -1)
#   CLOUDFLARED_PID=$(echo "$output" | tail -1)
#   trap "kill $CLOUDFLARED_PID 2>/dev/null" EXIT

set -euo pipefail

url=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        --url) url="$2"; shift 2 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

if [[ -z "$url" ]] || ! echo "$url" | grep -qE '^https?://'; then
    echo "Usage: $0 --url <http(s)://...>" >&2
    exit 1
fi

# install cloudflared if not present
if ! command -v cloudflared &>/dev/null; then
    echo "cloudflared not found, installing..." >&2
    case "$(uname -s)" in
    Darwin)
        brew install cloudflared ;;
    Linux)
        case "$(uname -m)" in
        x86_64)  ARCH="amd64" ;;
        aarch64) ARCH="arm64" ;;
        *)
            echo "ERROR: unsupported Linux architecture $(uname -m)." >&2
            exit 1 ;;
        esac
        curl -fsSL "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-$ARCH" -o /usr/local/bin/cloudflared
        chmod +x /usr/local/bin/cloudflared ;;
    *)
        echo "ERROR: unsupported OS. Install cloudflared manually." >&2
        exit 1 ;;
    esac
fi

IS_HTTPS=false
if echo "$url" | grep -q '^https://'; then
    IS_HTTPS=true
fi

TUNNEL_LOG=$(mktemp)

echo "Starting cloudflared tunnel to $url..." >&2

if [ "$IS_HTTPS" = true ]; then
    cloudflared tunnel --url "$url" --no-tls-verify > "$TUNNEL_LOG" 2>&1 &
else
    cloudflared tunnel --url "$url" > "$TUNNEL_LOG" 2>&1 &
fi
TUNNEL_PID=$!

TUNNEL_URL=""
for i in $(seq 1 15); do
    TUNNEL_URL=$(grep -o 'https://[a-zA-Z0-9\-]*\.trycloudflare\.com' "$TUNNEL_LOG" || true)
    if [ -n "$TUNNEL_URL" ]; then
        break
    fi
    sleep 1
done

rm -f "$TUNNEL_LOG"

if [ -z "$TUNNEL_URL" ]; then
    # kill the cloudflared process since we can't return a valid URL to the caller
    kill $TUNNEL_PID 2>/dev/null
    echo "ERROR: cloudflared tunnel failed to start." >&2
    exit 1
fi

echo "cloudflared tunnel: $TUNNEL_URL -> $url (PID $TUNNEL_PID)" >&2

# stdout: URL on line 1, PID on line 2 — caller parses these
echo "$TUNNEL_URL"
echo "$TUNNEL_PID"

#!/bin/bash

# MCP Server Tools List Script
# This script enumerates all tools available from the MCP server

# Default server URL
SERVER_URL="${MCP_SERVER_URL:-http://localhost:5000}/mcp"

echo "Enumerating MCP Server Tools..."
echo "Server URL: $SERVER_URL"
echo "================================"
echo ""

# Call the tools/list endpoint
# MCP servers expose their tools via the root / endpoint
# The response is in SSE format, so we need to extract the JSON from the event data
curl -s -X POST "$SERVER_URL/" \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/list",
    "params": {}
  }' | grep "^data:" | sed 's/^data: //' | jq '.'

echo ""
echo "================================"
echo "Done!"

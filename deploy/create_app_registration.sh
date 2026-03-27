#!/usr/bin/env bash
set -euo pipefail

echo "Script to create an Azure Entra ID App Registration for the MCP Server OAuth proxy"
echo ""
echo "Prerequisites:"
echo "  1. Activate 'Application Administrator' (or Global Admin) role via PIM if required by your org"
echo "  2. Run 'az login' with the activated role"
echo ""
echo "Usage:  --tenantId <value> --appName <value> --keyVaultName <value> [--spaRedirectUris <value>] [--webRedirectUris <value>] [--secretName <value>]"
echo ""

# Defaults
spaRedirectUris="http://localhost,http://127.0.0.1"
webRedirectUris="https://claude.ai/api/mcp/auth_callback"
secretName="mcp-kusto-server-client-secret"
tenantId="" appName="" keyVaultName=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --tenantId)        tenantId="$2"; shift 2 ;;
        --appName)         appName="$2"; shift 2 ;;
        --keyVaultName)    keyVaultName="$2"; shift 2 ;;
        --spaRedirectUris) spaRedirectUris="$2"; shift 2 ;;
        --webRedirectUris) webRedirectUris="$2"; shift 2 ;;
        --secretName)      secretName="$2"; shift 2 ;;
        *) echo "Unknown argument: $1"; exit 1 ;;
    esac
done

if [[ -z "$tenantId" || -z "$appName" || -z "$keyVaultName" ]]; then
    echo "ERROR: --tenantId, --appName, and --keyVaultName are required."
    exit 1
fi

echo "Creating Entra ID App Registration: $appName"

echo "Checking if app registration already exists..."
existingAppId=$(az ad app list --display-name "$appName" --query "[0].appId" -o tsv 2>/dev/null || echo "")

if [ -n "$existingAppId" ] && [ "$existingAppId" != "None" ]; then
    echo "App registration '$appName' already exists with appId: $existingAppId"
    appId=$existingAppId
else
    echo "Creating new app registration..."
    appId=$(az ad app create \
        --display-name "$appName" \
        --sign-in-audience AzureADMyOrg \
        --query appId -o tsv)
    echo "App registration created with appId: $appId"
fi

echo "Retrieving object ID..."
objectId=$(az ad app show --id "$appId" --query id -o tsv)

# SPA redirect URIs (for local dev with PKCE)
echo "Configuring SPA redirect URIs..."
IFS=',' read -ra SPA_URI_ARRAY <<< "$spaRedirectUris"
spaUriJson=$(printf '%s\n' "${SPA_URI_ARRAY[@]}" | jq -R . | jq -s .)

# Web redirect URIs (for Claude's server-side callback)
echo "Configuring Web redirect URIs..."
IFS=',' read -ra WEB_URI_ARRAY <<< "$webRedirectUris"
webUriJson=$(printf '%s\n' "${WEB_URI_ARRAY[@]}" | jq -R . | jq -s .)

# SPA redirect URIs for local dev (PKCE, browser-based).
# Web redirect URIs for Claude's server-side callback (requires client_secret).
# Note: SPA tokens can only be redeemed via cross-origin (browser) requests,
# so server-side token exchange requires Web platform + client_secret.
az rest --method PATCH \
    --uri "https://graph.microsoft.com/v1.0/applications/$objectId" \
    --headers "Content-Type=application/json" \
    --body "{\"spa\":{\"redirectUris\":$spaUriJson},\"web\":{\"redirectUris\":$webUriJson}}"
echo "Redirect URIs configured (SPA: $spaRedirectUris, Web: $webRedirectUris)"

# Add Azure Data Explorer delegated permission (user_impersonation)
echo "Adding Azure Data Explorer API permission..."
ADX_APP_ID="2746ea77-4702-4b45-80ca-3c97e680e8b7"
ADX_USER_IMPERSONATION_ID="00d678f0-da44-4b12-a6d6-c98bcfd1c5fe"
az rest --method PATCH \
    --uri "https://graph.microsoft.com/v1.0/applications/$objectId" \
    --headers "Content-Type=application/json" \
    --body "{\"requiredResourceAccess\":[{\"resourceAppId\":\"$ADX_APP_ID\",\"resourceAccess\":[{\"id\":\"$ADX_USER_IMPERSONATION_ID\",\"type\":\"Scope\"}]}]}" \
    && echo "Azure Data Explorer user_impersonation permission added" \
    || echo "WARNING: Could not add ADX API permission. Add it manually in Azure Portal > API permissions."

echo "Checking for existing client secret..."
existingSecrets=$(az ad app credential list --id "$appId" --query "length(@)" -o tsv)

if [ "$existingSecrets" -gt 0 ]; then
    echo "WARNING: App already has $existingSecrets secret(s). Skipping secret creation."
    echo "To create a new secret, run: az ad app credential reset --id $appId"
else
    echo "Creating client secret..."
    clientSecret=$(az ad app credential reset \
        --id "$appId" \
        --display-name "mcp-server-secret" \
        --years 1 \
        --query password -o tsv)
    echo "Client secret created (valid for 1 year)"

    echo "Storing client secret in Key Vault $keyVaultName/$secretName..."
    az keyvault secret set --vault-name "$keyVaultName" -n "$secretName" --value "$clientSecret"
    echo "Vault secret $keyVaultName/$secretName stored"
fi

echo ""
echo "App Registration setup completed"
echo ""
echo "Environment variables for the MCP server:"
echo ""
echo "  export AZURE_TENANT_ID=$tenantId"
echo "  export OAuth_ClientId=$appId"
echo "  export OAuth_ClientSecret=<retrieve from Key Vault: $keyVaultName/$secretName>"
echo ""
echo "IMPORTANT: On first use, users will be prompted to consent to Azure Data Explorer access."
echo "An admin can pre-consent for the org: az ad app permission admin-consent --id $appId"
echo ""
echo "Client secret stored in Key Vault: $keyVaultName/$secretName"

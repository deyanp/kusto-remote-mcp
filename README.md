# KustoRemoteMcp

A remote MCP (Model Context Protocol) server that lets Claude query Azure Data Explorer (Kusto) under the authenticated user's own identity. The server acts as an OAuth proxy between Claude and Azure Entra ID, so each user's ADX permissions, row-level security, and audit trail are preserved — no shared service principal is involved.

For more info read the post at dev.to [Claude and Kusto or MCP and CQRS+](https://dev.to/deyanp/claude-and-kusto-or-mcp-and-cqrs-4n2j).

## How it works

```
Claude Desktop ──OAuth 2.1──> MCP Server ──OAuth 2.0──> Azure Entra ID
                                  │
                                  └── KQL query ──> Azure Data Explorer
                                      (user's own Bearer token)
```

The server presents standard OAuth endpoints to Claude, proxies authentication to Entra ID (rewriting scopes and injecting credentials), and passes the resulting user token directly to ADX via `WithAadUserTokenAuthentication`.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) (`az login`)
- An Azure Entra ID tenant
- An Azure Data Explorer cluster and database
- [cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/) for local tunneling (`brew install cloudflared`)

## Setup

### 1. Create the Entra ID App Registration

```bash
./deploy/create_app_registration.sh \
  --tenantId <your-tenant-id> \
  --appName mcp-kusto-server-dev \
  --keyVaultName <your-key-vault>
```

This creates (or reuses) an app registration with:
- SPA redirect URIs for local dev (`http://localhost`, `http://127.0.0.1`)
- Web redirect URI for Claude (`https://claude.ai/api/mcp/auth_callback`)
- Azure Data Explorer `user_impersonation` delegated permission
- Client secret stored in Key Vault

To do it manually: Azure Portal > App registrations > New > single tenant, then add the redirect URIs, API permission, and client secret above.

### 2. Grant Kusto database access

Each user who will query via Claude needs access to the database:

```kql
.add database <your-database> viewers ('aaduser=user@yourdomain.com')
```

### 3. Configure environment

Create `launchSettings.json` in the `src/` folder:

```json
{
    "profiles": {
        "LocalDev": {
            "commandName": "Project",
            "environmentVariables": {
                "AZURE_TENANT_ID": "<your-tenant-id>",
                "OAuth_ClientId": "<your-client-id>",
                "OAuth_ClientSecret": "!@Microsoft.KeyVault(SecretUri=https://<your-vault>.vault.azure.net/secrets/mcp-kusto-server-client-secret/)",
                "AzureDataExplorer_ConnectionString": "https://<your-cluster>.kusto.windows.net",
                "AzureDataExplorer_Database": "<your-database>"
            },
            "applicationUrl": "https://localhost:5001"
        }
    }
}
```

| Variable | Description |
|----------|-------------|
| `AZURE_TENANT_ID` | Entra ID tenant ID |
| `OAuth_ClientId` | App registration client ID (not `AZURE_CLIENT_ID` — that conflicts with `DefaultAzureCredential`) |
| `OAuth_ClientSecret` | Client secret or Key Vault reference |
| `AzureDataExplorer_ConnectionString` | Kusto cluster URL |
| `AzureDataExplorer_Database` | Kusto database name |

### 4. Run locally

```bash
./dotnet_run.sh --mcp true
```

This loads env vars from `launchSettings.json`, starts a Cloudflare tunnel, and runs the server. The tunnel URL is printed — use it as a connector in Claude Desktop.

Without the tunnel (server-only):

```bash
./dotnet_run.sh
```

### 5. Connect Claude Desktop

1. **Settings** > **Connectors** > **Add Connector**
2. Paste the tunnel URL from the previous step (e.g. `https://xxx-yyy.trycloudflare.com/mcp`)
3. Authenticate with Entra ID when prompted
4. Use the `execute_kusto_query` tool to run KQL queries

/// Application configuration loaded from environment variables (tenant, OAuth, ADX, URLs).
module EnvVars

open Framework.Configuration

let tenantId = Environment.getEnvironmentVariable "AZURE_TENANT_ID"

module OAuth =
    let clientId = Environment.getEnvironmentVariable "OAuth_ClientId"
    let clientSecret = Environment.getEnvironmentVariable "OAuth_ClientSecret"

let listenUrl =
    Environment.tryGetEnvironmentVariable "MCP_LISTEN_URL"
    |> Option.defaultValue "https://localhost:5001"

let baseUrl =
    Environment.tryGetEnvironmentVariable "MCP_BASE_URL"
    |> Option.defaultValue listenUrl

module ADX =
    let connectionString =
        Environment.getEnvironmentVariable "AzureDataExplorer_ConnectionString"

    let database = Environment.getEnvironmentVariable "AzureDataExplorer_Database"
    let serviceUrl = $"%s{connectionString}/%s{database}"

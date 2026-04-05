/// Application configuration loaded from environment variables (tenant, OAuth, ADX, URLs).
module KustoRemoteMcp.EnvVars

open Framework.Configuration

type EntraIdConfig =
    { TenantId: string
      ClientId: string
      ClientSecret: string }

module EntraIdConfig =
    let fromEnv () =
        { TenantId = Environment.getEnvironmentVariable "AZURE_TENANT_ID"
          ClientId = Environment.getEnvironmentVariable "OAuth_ClientId"
          ClientSecret = Environment.getEnvironmentVariable "OAuth_ClientSecret" }

type AdxConfig =
    { ConnectionString: string
      Database: string
      ServiceUrl: string }

module AdxConfig =
    let fromEnv () =
        let conn = Environment.getEnvironmentVariable "AzureDataExplorer_ConnectionString"
        let db = Environment.getEnvironmentVariable "AzureDataExplorer_Database"

        { ConnectionString = conn
          Database = db
          ServiceUrl = $"%s{conn}/%s{db}" }

type ServerConfig = { ListenUrl: string; BaseUrl: string }

module ServerConfig =
    let fromEnv () =
        let listenUrl =
            Environment.tryGetEnvironmentVariable "MCP_LISTEN_URL"
            |> Option.defaultValue "https://localhost:5001"

        let baseUrl =
            Environment.tryGetEnvironmentVariable "MCP_BASE_URL"
            |> Option.defaultValue listenUrl

        { ListenUrl = listenUrl
          BaseUrl = baseUrl }

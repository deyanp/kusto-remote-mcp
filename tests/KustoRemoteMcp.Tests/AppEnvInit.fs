module KustoRemoteMcp.Tests.AppEnvInit

open KustoRemoteMcp.AppEnv

let envVars =
    Map.ofList
        [ "AZURE_TENANT_ID", "test-tenant-00000000-0000-0000-0000-000000000000"
          "OAuth_ClientId", "test-client-id"
          "OAuth_ClientSecret", "test-client-secret"
          "AzureDataExplorer_ConnectionString", "https://testcluster.kusto.windows.net"
          "AzureDataExplorer_Database", "testdb"
          "MCP_BASE_URL", "https://test-mcp.example.com" ]

let appEnv = AppEnv.create envVars

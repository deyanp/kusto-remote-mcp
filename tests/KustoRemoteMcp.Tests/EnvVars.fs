module KustoRemoteMcp.Tests.EnvVars

open KustoRemoteMcp.EnvVars

let testEntra: EntraIdConfig =
    { TenantId = "test-tenant-00000000-0000-0000-0000-000000000000"
      ClientId = "test-client-id"
      ClientSecret = "test-client-secret" }

let testAdx: AdxConfig =
    { ConnectionString = "https://testcluster.kusto.windows.net"
      Database = "testdb"
      ServiceUrl = "https://testcluster.kusto.windows.net/testdb" }

let testServer: ServerConfig =
    { ListenUrl = "https://localhost:5001"
      BaseUrl = "https://test-mcp.example.com" }

let testScopeString =
    sprintf "%s/.default openid profile offline_access" testAdx.ConnectionString

let testScopeArray =
    [| sprintf "%s/.default" testAdx.ConnectionString
       "openid"
       "profile"
       "offline_access" |]

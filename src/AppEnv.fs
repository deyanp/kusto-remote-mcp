/// Application environment: all configuration and eagerly-created clients, constructed from an env var map.
module KustoRemoteMcp.AppEnv

open System.Net.Http
open Framework.Configuration

type AppEnv =
    { TenantId: string
      ClientId: string
      ClientSecret: string
      AdxConnectionString: string
      AdxDatabase: string
      AdxServiceUrl: string
      ListenUrl: string
      BaseUrl: string
      ScopeString: string
      ScopeArray: string[]
      EntraTokenEndpointUrl: string
      HttpClient: HttpClient }

module AppEnv =
    let create (envVars: Map<string, string>) =
        let tenantId = Environment.getFromMap envVars "AZURE_TENANT_ID"
        let clientId = Environment.getFromMap envVars "OAuth_ClientId"
        let clientSecret = Environment.getFromMap envVars "OAuth_ClientSecret"
        let conn = Environment.getFromMap envVars "AzureDataExplorer_ConnectionString"
        let db = Environment.getFromMap envVars "AzureDataExplorer_Database"

        let listenUrl =
            Environment.tryGetFromMap envVars "MCP_LISTEN_URL"
            |> Option.defaultValue "https://localhost:5001"

        let baseUrl =
            Environment.tryGetFromMap envVars "MCP_BASE_URL"
            |> Option.defaultValue listenUrl

        { TenantId = tenantId
          ClientId = clientId
          ClientSecret = clientSecret
          AdxConnectionString = conn
          AdxDatabase = db
          AdxServiceUrl = $"%s{conn}/%s{db}"
          ListenUrl = listenUrl
          BaseUrl = baseUrl
          ScopeString = sprintf "%s/.default openid profile offline_access" conn
          ScopeArray = [| sprintf "%s/.default" conn; "openid"; "profile"; "offline_access" |]
          EntraTokenEndpointUrl = sprintf "https://login.microsoftonline.com/%s/oauth2/v2.0/token" tenantId
          HttpClient = new HttpClient() }

/// Dependency composition: partial application of configuration, infrastructure, and logging into MCP tools and OAuth.
module KustoRemoteMcp.DependencyInjection

open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Kusto.Data
open Kusto.Data.Net.Client
open EnvVars

let private log = Framework.Logging.createDefault "KustoRemoteMcp.MCP"

module McpTools =
    let executeKustoQuery (adx: AdxConfig) =
        let accessor = HttpContextAccessor() :> IHttpContextAccessor
        let getAuthorizationHeader = Framework.Http.getAuthorizationHeader accessor

        let createAdxClient (userToken: string) : Kusto.Data.Common.ICslQueryProvider =
            KustoConnectionStringBuilder(adx.ServiceUrl).WithAadUserTokenAuthentication(userToken)
            |> KustoClientFactory.CreateCslQueryProvider

        fun (query: string) ->
            Api.Functions.McpTools.executeKustoQuery log getAuthorizationHeader createAdxClient query
            |> Async.StartAsTask

module OAuth =
    let private scopeString (adx: AdxConfig) =
        sprintf "%s/.default openid profile offline_access" adx.ConnectionString

    let private scopeArray (adx: AdxConfig) =
        [| sprintf "%s/.default" adx.ConnectionString
           "openid"
           "profile"
           "offline_access" |]

    let private callEntraTokenEndpoint (entra: EntraIdConfig) =
        let httpClient = new HttpClient()

        Framework.Http.callOverHttp
            httpClient
            (sprintf "https://login.microsoftonline.com/%s/oauth2/v2.0/token" entra.TenantId)

    let register (entra: EntraIdConfig) =
        Api.Functions.OAuth.register entra.ClientId log

    let authorize (entra: EntraIdConfig) (adx: AdxConfig) =
        Api.Functions.OAuth.authorize entra.TenantId entra.ClientId (scopeString adx) log

    let token (entra: EntraIdConfig) (adx: AdxConfig) =
        Api.Functions.OAuth.token entra.ClientId entra.ClientSecret (scopeString adx) (callEntraTokenEndpoint entra) log

    let wellKnownOauthProtectedResource (adx: AdxConfig) (server: ServerConfig) =
        Api.Functions.OAuth.wellKnownOauthProtectedResource server.BaseUrl (scopeArray adx) log

    let wellKnownAuthServer (entra: EntraIdConfig) (adx: AdxConfig) (server: ServerConfig) =
        Api.Functions.OAuth.wellKnownAuthServer entra.TenantId entra.ClientId server.BaseUrl (scopeArray adx) log

module Middleware =
    let createBearerTokenAuth (entra: EntraIdConfig) (server: ServerConfig) =
        Framework.AzureEntraIdOAuth.BearerTokenMiddleware.requireBearerToken
            entra.TenantId
            "MCP Server"
            (sprintf "%s/.well-known/oauth-protected-resource" server.BaseUrl)
            [ "/mcp" ]

/// Dependency composition: partial application of AppEnv into MCP tools, OAuth handlers, and middleware.
module KustoRemoteMcp.DependencyInjection

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Kusto.Data
open Kusto.Data.Net.Client
open AppEnv

let private log = Framework.Logging.createDefault "KustoRemoteMcp.MCP"

module McpTools =
    let executeKustoQuery (appEnv: AppEnv) =
        let accessor = HttpContextAccessor() :> IHttpContextAccessor
        let getAuthorizationHeader = Framework.Http.getAuthorizationHeader accessor

        let createAdxClient (userToken: string) : Kusto.Data.Common.ICslQueryProvider =
            KustoConnectionStringBuilder(appEnv.AdxServiceUrl).WithAadUserTokenAuthentication(userToken)
            |> KustoClientFactory.CreateCslQueryProvider

        fun (query: string) ->
            Api.Functions.McpTools.executeKustoQuery log getAuthorizationHeader createAdxClient query
            |> Async.StartAsTask

module OAuth =
    let private callEntraTokenEndpoint (appEnv: AppEnv) =
        Framework.Http.callOverHttp appEnv.HttpClient appEnv.EntraTokenEndpointUrl

    let register (appEnv: AppEnv) =
        Api.Functions.OAuth.register appEnv.ClientId log

    let authorize (appEnv: AppEnv) =
        Api.Functions.OAuth.authorize appEnv.TenantId appEnv.ClientId appEnv.ScopeString log

    let token (appEnv: AppEnv) =
        Api.Functions.OAuth.token appEnv.ClientId appEnv.ClientSecret appEnv.ScopeString (callEntraTokenEndpoint appEnv) log

    let wellKnownOauthProtectedResource (appEnv: AppEnv) =
        Api.Functions.OAuth.wellKnownOauthProtectedResource appEnv.BaseUrl appEnv.ScopeArray log

    let wellKnownAuthServer (appEnv: AppEnv) =
        Api.Functions.OAuth.wellKnownAuthServer appEnv.TenantId appEnv.ClientId appEnv.BaseUrl appEnv.ScopeArray log

module Middleware =
    let createBearerTokenAuth (appEnv: AppEnv) =
        Framework.AzureEntraIdOAuth.BearerTokenMiddleware.requireBearerToken
            appEnv.TenantId
            "MCP Server"
            (sprintf "%s/.well-known/oauth-protected-resource" appEnv.BaseUrl)
            [ "/mcp" ]

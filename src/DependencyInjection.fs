/// Dependency composition: partial application of configuration, infrastructure, and logging into MCP tools and OAuth.
module MCP.DependencyInjection

open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Kusto.Data
open Kusto.Data.Net.Client
open Framework.Mcp.Hosting

let private log = Framework.Logging.createDefault "KustoRemoteMcp.MCP"

let private callEntraTokenEndpoint =
    let httpClient = new HttpClient()

    Framework.Http.callOverHttp
        httpClient
        (sprintf "https://login.microsoftonline.com/%s/oauth2/v2.0/token" EnvVars.tenantId)

let configureMcpServices: McpServerToolDef list -> IHostBuilder -> IHostBuilder =
    Framework.Mcp.Hosting.configureMcpServices

// Direct instantiation instead of DI: HttpContextAccessor uses a static AsyncLocal internally,
// so this instance is functionally identical to the one registered via services.AddHttpContextAccessor().
// A module-level instance is safe for concurrent requests — each async context sees its own HttpContext.
let private accessor = HttpContextAccessor() :> IHttpContextAccessor

module McpTools =
    let private getAuthorizationHeader = Framework.Http.getAuthorizationHeader accessor

    let private createAdxClient (userToken: string) : Kusto.Data.Common.ICslQueryProvider =
        KustoConnectionStringBuilder(EnvVars.ADX.serviceUrl).WithAadUserTokenAuthentication(userToken)
        |> KustoClientFactory.CreateCslQueryProvider

    let executeKustoQuery (query: string) : Task<string> =
        MCP.Api.Functions.McpTools.executeKustoQuery log getAuthorizationHeader createAdxClient query
        |> Async.StartAsTask

module OAuth =
    let requireBearerToken: IApplicationBuilder -> unit =
        Framework.AzureEntraIdOAuth.BearerTokenMiddleware.requireBearerToken
            EnvVars.tenantId
            "MCP Server"
            (sprintf "%s/.well-known/oauth-protected-resource" EnvVars.baseUrl)
            [ "/mcp" ]

    // ADX-specific scopes — the resource server identity + standard OIDC scopes
    let private scopeString =
        sprintf "%s/.default openid profile offline_access" EnvVars.ADX.connectionString

    let private scopeArray =
        [| sprintf "%s/.default" EnvVars.ADX.connectionString
           "openid"
           "profile"
           "offline_access" |]

    let register: HttpContext -> Async<unit> =
        Api.Functions.OAuth.register EnvVars.OAuth.clientId log

    let authorize: HttpContext -> Async<unit> =
        Api.Functions.OAuth.authorize EnvVars.tenantId EnvVars.OAuth.clientId scopeString log

    let token: HttpContext -> Async<unit> =
        Api.Functions.OAuth.token
            EnvVars.OAuth.clientId
            EnvVars.OAuth.clientSecret
            scopeString
            callEntraTokenEndpoint
            log

    let wellKnownOauthProtectedResource: HttpContext -> Async<unit> =
        Api.Functions.OAuth.wellKnownOauthProtectedResource EnvVars.baseUrl scopeArray log

    let wellKnownAuthServer: HttpContext -> Async<unit> =
        Api.Functions.OAuth.wellKnownAuthServer EnvVars.tenantId EnvVars.OAuth.clientId EnvVars.baseUrl scopeArray log

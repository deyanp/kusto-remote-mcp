/// Web API endpoint definitions and MCP tool definitions that map to the host builder.
namespace Api.Wiring

open System
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Framework.Hosting.HostBuilder
open Framework.Mcp.Hosting

module McpDI = MCP.DependencyInjection

module WebApi =
    let health =
        { Name = "health"
          Method = HttpMethod.Get
          Path = "/health"
          ExecuteOperation =
            fun (ctx: HttpContext) ->
                task {
                    ctx.Response.StatusCode <- 200
                    do! ctx.Response.WriteAsync("OK")
                }
                :> Task }

    module OAuth =
        let register =
            { Name = "oauth_register"
              Method = HttpMethod.Post
              Path = "/oauth/register"
              ExecuteOperation = McpDI.OAuth.register >> fun a -> Async.StartAsTask a :> Task }

        let authorize =
            { Name = "oauth_authorize"
              Method = HttpMethod.Get
              Path = "/oauth/authorize"
              ExecuteOperation = McpDI.OAuth.authorize >> fun a -> Async.StartAsTask a :> Task }

        let token =
            { Name = "oauth_token"
              Method = HttpMethod.Post
              Path = "/oauth/token"
              ExecuteOperation = McpDI.OAuth.token >> fun a -> Async.StartAsTask a :> Task }

        let wellKnownProtectedResource =
            { Name = "oauth_resource"
              Method = HttpMethod.Get
              Path = "/.well-known/oauth-protected-resource"
              ExecuteOperation =
                McpDI.OAuth.wellKnownOauthProtectedResource
                >> fun a -> Async.StartAsTask a :> Task }

        let wellKnownAuthServer =
            { Name = "oauth_server"
              Method = HttpMethod.Get
              Path = "/.well-known/oauth-authorization-server"
              ExecuteOperation = McpDI.OAuth.wellKnownAuthServer >> fun a -> Async.StartAsTask a :> Task }

module McpTools =
    let executeKustoQuery: McpServerToolDef =
        { Name = "execute_kusto_query"
          Description =
            "Executes a KQL query against the configured Azure Data Explorer (Kusto) cluster and database. Returns query results as a JSON array. The query runs under the authenticated user's identity."
          ReadOnly = true
          Destructive = false
          ExecuteOperation = Func<string, Task<string>>(McpDI.McpTools.executeKustoQuery) }

/// Web API endpoint definitions and MCP tool definitions that map to the host builder.
namespace KustoRemoteMcp.Api.Wiring

open System
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Framework.Hosting.HostBuilder
open Framework.Mcp.Hosting

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
        let private wrapAsync (handler: HttpContext -> Async<unit>) : HttpContext -> Task =
            handler >> Async.StartAsTask >> (fun t -> t :> Task)

        let register (handler: HttpContext -> Async<unit>) =
            { Name = "oauth_register"
              Method = HttpMethod.Post
              Path = "/oauth/register"
              ExecuteOperation = wrapAsync handler }

        let authorize (handler: HttpContext -> Async<unit>) =
            { Name = "oauth_authorize"
              Method = HttpMethod.Get
              Path = "/oauth/authorize"
              ExecuteOperation = wrapAsync handler }

        let token (handler: HttpContext -> Async<unit>) =
            { Name = "oauth_token"
              Method = HttpMethod.Post
              Path = "/oauth/token"
              ExecuteOperation = wrapAsync handler }

        let wellKnownProtectedResource (handler: HttpContext -> Async<unit>) =
            { Name = "oauth_resource"
              Method = HttpMethod.Get
              Path = "/.well-known/oauth-protected-resource"
              ExecuteOperation = wrapAsync handler }

        let wellKnownAuthServer (handler: HttpContext -> Async<unit>) =
            { Name = "oauth_server"
              Method = HttpMethod.Get
              Path = "/.well-known/oauth-authorization-server"
              ExecuteOperation = wrapAsync handler }


module McpTools =
    let executeKustoQuery (handler: string -> Task<string>) =
        { Name = "execute_kusto_query"
          Description =
            "Executes a KQL query against the configured Azure Data Explorer (Kusto) cluster and database. Returns query results as a JSON array. The query runs under the authenticated user's identity."
          ReadOnly = true
          Destructive = false
          ExecuteOperation = Func<string, Task<string>>(handler) }
